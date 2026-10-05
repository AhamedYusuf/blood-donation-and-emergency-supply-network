# Performance Test Report

**Run at:** 2026-10-04T15:35:49.462486+00:00

Real HTTP requests against the live backend + PostgreSQL + agent-service (not mocked, not in-memory). Run locally with the full stack up.

## GET /api/organizations

Lightweight authenticated read (baseline, minimal DB work) — 20 concurrent requests

- Concurrency: 20
- Total requests: 100
- Success rate: 100.0% (100/100)
- Latency: min 0.9ms, avg 26.8ms, p95 131.7ms, max 134.8ms

## GET /api/appointments/bloodbank/{orgId}/upcoming

Business-data read with a batched DonorProfile join + AgentStep lookup per row — measures real database response under concurrency

- Concurrency: 20
- Total requests: 100
- Success rate: 100.0% (100/100)
- Latency: min 7.0ms, avg 16.3ms, p95 37.5ms, max 42.9ms

## POST /api/internal/agent/search-donors

A single agent operation in isolation (Haversine distance + ranking over real donor rows) — the 'Agentic AI latency' for one tool call, not a full workflow

- Concurrency: 10
- Total requests: 50
- Success rate: 100.0% (50/50)
- Latency: min 2.4ms, avg 5.3ms, p95 7.2ms, max 7.7ms

## Full agent-workflow orchestration latency

`POST /api/requests` — creates a blood request and synchronously runs the Coordinator through stock_check -> search_donors -> validate_eligibility -> mark_awaiting_approval before returning. This is the real, end-to-end **Agentic AI latency** for one complete planning-and-delegation run, not a single tool call.

- Runs measured: 10
- Latency: min 209.3ms, avg 237.9ms, max 298.4ms
