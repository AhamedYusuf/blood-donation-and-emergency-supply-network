#!/usr/bin/env python3
"""
Performance test — Assignment 1 spec §12 ("Performance: Concurrent
requests, response time, success/failure rate, database response and
Agentic AI latency").

This drives the REAL, running stack (backend + PostgreSQL + agent-service)
over HTTP — it is not a mock or an in-memory benchmark, so the numbers it
produces reflect actual network + DB + agent-orchestration latency.

Requires, running locally, before you start this:
    - PostgreSQL with the seeded dev data (see CLAUDE.md's test accounts)
    - The ASP.NET Core API on http://localhost:5067
    - The agent-service on http://localhost:8000

Usage:
    cd testing/performance
    python3 -m venv .venv && source .venv/bin/activate
    pip install requests
    python3 run_load_test.py

Produces:
    - A results table printed to stdout
    - performance_report.md, a ready-to-paste report with real numbers,
      a timestamp, and the exact scenarios/parameters used
"""

from __future__ import annotations

import json
import statistics
import time
import uuid
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from datetime import datetime, timezone

import requests

BACKEND_BASE_URL = "http://localhost:5067"
AGENT_SERVICE_BASE_URL = "http://localhost:8000"
INTERNAL_AGENT_SECRET = "dev-only-local-secret-value-12345"
TEST_ORG_ID = "11111111-1111-1111-1111-111111111111"

STAFF_EMAIL = "staff1@example.com"
STAFF_PASSWORD = "TestPass123!"


@dataclass
class RequestResult:
    ok: bool
    status_code: int | None
    latency_ms: float
    error: str | None = None


@dataclass
class ScenarioResult:
    name: str
    description: str
    concurrency: int
    total_requests: int
    results: list[RequestResult] = field(default_factory=list)

    @property
    def successes(self) -> list[RequestResult]:
        return [r for r in self.results if r.ok]

    @property
    def failures(self) -> list[RequestResult]:
        return [r for r in self.results if not r.ok]

    @property
    def success_rate(self) -> float:
        if not self.results:
            return 0.0
        return 100.0 * len(self.successes) / len(self.results)

    def latency_stats(self) -> dict[str, float]:
        latencies = sorted(r.latency_ms for r in self.successes)
        if not latencies:
            return {"min": 0, "avg": 0, "p95": 0, "max": 0}
        p95_index = min(len(latencies) - 1, int(len(latencies) * 0.95))
        return {
            "min": latencies[0],
            "avg": statistics.mean(latencies),
            "p95": latencies[p95_index],
            "max": latencies[-1],
        }


def login(email: str, password: str) -> str:
    resp = requests.post(
        f"{BACKEND_BASE_URL}/api/auth/login",
        json={"email": email, "password": password},
        timeout=10,
    )
    resp.raise_for_status()
    return resp.json()["accessToken"]


def timed_request(fn) -> RequestResult:
    started = time.perf_counter()
    try:
        resp = fn()
        latency_ms = (time.perf_counter() - started) * 1000
        ok = resp.status_code < 400
        return RequestResult(ok=ok, status_code=resp.status_code, latency_ms=latency_ms)
    except Exception as exc:  # noqa: BLE001 - a failed request is a valid, measured outcome
        latency_ms = (time.perf_counter() - started) * 1000
        return RequestResult(ok=False, status_code=None, latency_ms=latency_ms, error=str(exc))


def run_scenario(name: str, description: str, concurrency: int, total_requests: int, request_fn) -> ScenarioResult:
    scenario = ScenarioResult(name=name, description=description, concurrency=concurrency, total_requests=total_requests)
    with ThreadPoolExecutor(max_workers=concurrency) as pool:
        futures = [pool.submit(timed_request, request_fn) for _ in range(total_requests)]
        for future in as_completed(futures):
            scenario.results.append(future.result())
    return scenario


def main() -> None:
    print("=" * 70)
    print("PERFORMANCE TEST — live stack, real HTTP, real DB, real agents")
    print("=" * 70)

    staff_token = login(STAFF_EMAIL, STAFF_PASSWORD)
    print(f"Logged in as {STAFF_EMAIL}\n")

    scenarios: list[ScenarioResult] = []

    # --- Scenario 1: lightweight authenticated read, no DB join ---
    scenarios.append(run_scenario(
        name="GET /api/organizations",
        description="Lightweight authenticated read (baseline, minimal DB work) — 20 concurrent requests",
        concurrency=20,
        total_requests=100,
        request_fn=lambda: requests.get(
            f"{BACKEND_BASE_URL}/api/organizations",
            headers={"Authorization": f"Bearer {staff_token}"},
            timeout=10,
        ),
    ))

    # --- Scenario 2: DB-heavier authenticated read (joins DonorProfiles/AgentSteps) ---
    scenarios.append(run_scenario(
        name="GET /api/appointments/bloodbank/{orgId}/upcoming",
        description="Business-data read with a batched DonorProfile join + AgentStep lookup per row — measures real database response under concurrency",
        concurrency=20,
        total_requests=100,
        request_fn=lambda: requests.get(
            f"{BACKEND_BASE_URL}/api/appointments/bloodbank/{TEST_ORG_ID}/upcoming?page=1&pageSize=20",
            headers={"Authorization": f"Bearer {staff_token}"},
            timeout=10,
        ),
    ))

    # --- Scenario 3: the agent's own core operation (internal, secret-protected) ---
    scenarios.append(run_scenario(
        name="POST /api/internal/agent/search-donors",
        description="A single agent operation in isolation (Haversine distance + ranking over real donor rows) — the 'Agentic AI latency' for one tool call, not a full workflow",
        concurrency=10,
        total_requests=50,
        request_fn=lambda: requests.post(
            f"{BACKEND_BASE_URL}/api/internal/agent/search-donors",
            headers={"X-Internal-Secret": INTERNAL_AGENT_SECRET},
            json={
                "workflowId": str(uuid.uuid4()),
                "bloodType": "O+",
                "unitsNeeded": 2,
                "location": {"lat": 6.9271, "lng": 79.8612},
                "urgencyLevel": "critical",
                "radiusKm": 50,
            },
            timeout=10,
        ),
    ))

    # --- Scenario 4: full multi-agent workflow orchestration latency ---
    # Sequential, not concurrent — this measures how long ONE complete
    # plan-and-delegate run takes end to end (stock_check -> search_donors
    # -> validate_eligibility -> mark_awaiting_approval), which is the
    # actual "Agentic AI latency" the spec is asking about, not a single
    # tool call. Run several times back-to-back to get a real distribution.
    print("Scenario: full /run-workflow orchestration latency (10 sequential runs)...")
    # POST /api/requests triggers /run-workflow server-side and returns
    # only once that call completes, so timing this request itself
    # captures the full orchestration run (stock_check -> search_donors
    # -> validate_eligibility -> mark_awaiting_approval), which is the
    # real "Agentic AI latency" the spec asks about — not a single tool
    # call. Sequential (not concurrent) since this is measuring one
    # complete plan-and-delegate run's own duration, repeated to get a
    # real distribution rather than a single sample.
    workflow_latencies: list[float] = []
    for i in range(10):
        started = time.perf_counter()
        req_resp = requests.post(
            f"{BACKEND_BASE_URL}/api/requests",
            headers={"Authorization": f"Bearer {staff_token}", "Content-Type": "application/json"},
            json={
                "organizationId": TEST_ORG_ID,
                "bloodType": "O+",
                "unitsRequested": 1,
                "urgency": "routine",
                "hospitalName": f"Perf Test Hospital {i}b",
                "latitude": 6.9271,
                "longitude": 79.8612,
                "notes": "Performance test run — safe to ignore/delete",
            },
            timeout=15,
        )
        latency_ms = (time.perf_counter() - started) * 1000
        if req_resp.status_code < 400:
            workflow_latencies.append(latency_ms)

    print()

    # --- Print results ---
    print("=" * 70)
    print("RESULTS")
    print("=" * 70)

    report_lines = [
        "# Performance Test Report",
        "",
        f"**Run at:** {datetime.now(timezone.utc).isoformat()}",
        "",
        "Real HTTP requests against the live backend + PostgreSQL + agent-service "
        "(not mocked, not in-memory). Run locally with the full stack up.",
        "",
    ]

    for scenario in scenarios:
        stats = scenario.latency_stats()
        print(f"\n### {scenario.name}")
        print(f"    {scenario.description}")
        print(f"    Concurrency: {scenario.concurrency}, Total requests: {scenario.total_requests}")
        print(f"    Success rate: {scenario.success_rate:.1f}% ({len(scenario.successes)}/{len(scenario.results)})")
        print(f"    Latency (ms): min={stats['min']:.1f}  avg={stats['avg']:.1f}  p95={stats['p95']:.1f}  max={stats['max']:.1f}")
        throughput = len(scenario.successes) / (sum(r.latency_ms for r in scenario.results) / 1000 / scenario.concurrency) if scenario.results else 0

        report_lines.extend([
            f"## {scenario.name}",
            "",
            scenario.description,
            "",
            f"- Concurrency: {scenario.concurrency}",
            f"- Total requests: {scenario.total_requests}",
            f"- Success rate: {scenario.success_rate:.1f}% ({len(scenario.successes)}/{len(scenario.results)})",
            f"- Latency: min {stats['min']:.1f}ms, avg {stats['avg']:.1f}ms, p95 {stats['p95']:.1f}ms, max {stats['max']:.1f}ms",
            "",
        ])

        if scenario.failures:
            sample_errors = {r.error or f"HTTP {r.status_code}" for r in scenario.failures}
            print(f"    Failure reasons observed: {sample_errors}")
            report_lines.append(f"- Failure reasons observed: {sample_errors}")
            report_lines.append("")

    if workflow_latencies:
        wf_avg = statistics.mean(workflow_latencies)
        wf_min = min(workflow_latencies)
        wf_max = max(workflow_latencies)
        print(f"\n### Full agent-workflow orchestration latency (POST /api/requests, "
              f"synchronously runs stock_check -> search_donors -> validate_eligibility)")
        print(f"    {len(workflow_latencies)} sequential runs")
        print(f"    Latency (ms): min={wf_min:.1f}  avg={wf_avg:.1f}  max={wf_max:.1f}")

        report_lines.extend([
            "## Full agent-workflow orchestration latency",
            "",
            "`POST /api/requests` — creates a blood request and synchronously runs "
            "the Coordinator through stock_check -> search_donors -> "
            "validate_eligibility -> mark_awaiting_approval before returning. "
            "This is the real, end-to-end **Agentic AI latency** for one complete "
            "planning-and-delegation run, not a single tool call.",
            "",
            f"- Runs measured: {len(workflow_latencies)}",
            f"- Latency: min {wf_min:.1f}ms, avg {wf_avg:.1f}ms, max {wf_max:.1f}ms",
            "",
        ])

    report_path = "performance_report.md"
    with open(report_path, "w") as f:
        f.write("\n".join(report_lines))

    print(f"\nReport written to {report_path}")


if __name__ == "__main__":
    main()
