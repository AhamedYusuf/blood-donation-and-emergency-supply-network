# Performance Testing

Assignment 1 §12 requires evidence of: concurrent requests, response time,
success/failure rate, database response, and Agentic AI latency.

This drives the **real, running stack** over HTTP — not a mock, not an
in-memory benchmark — so the numbers reflect actual network + PostgreSQL +
agent-orchestration latency.

## Prerequisites

Start the full stack first (see the root `CLAUDE.md`/each service's own
README for exact commands):

1. PostgreSQL running, with the seeded dev test accounts.
2. The ASP.NET Core API on `http://localhost:5067`.
3. The agent-service on `http://localhost:8000`.

## Run

```bash
cd testing/performance
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
python3 run_load_test.py
```

## What it measures

| Scenario | What it shows |
|---|---|
| `GET /api/organizations` | Baseline authenticated-read latency under 20 concurrent requests |
| `GET /api/appointments/bloodbank/{orgId}/upcoming` | Real database response time for a business-data read that joins `DonorProfiles`/`AgentSteps`, under concurrency |
| `POST /api/internal/agent/search-donors` | A single agent tool call's own latency (Haversine + ranking over real donor rows) |
| `POST /api/requests` (10 sequential runs) | The full multi-agent orchestration latency for one complete plan-and-delegate run (stock_check → search_donors → validate_eligibility → mark_awaiting_approval) — this is the actual **Agentic AI latency** the spec asks about, not a single tool call |

Each run writes `performance_report.md` with real timestamps and numbers,
ready to paste into the assignment's Performance Report section.

## Note

The workflow-orchestration scenario creates 10 real `BloodRequest` rows
(hospital name prefixed `Perf Test Hospital`) and their agent workflows in
whatever database you point it at — harmless test data, safe to leave or
delete.
