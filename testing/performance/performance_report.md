# Performance Test Report

**Run at:** 2026-09-26T09:14:22.116049+00:00

Real HTTP requests against the live backend + PostgreSQL + agent-service (not mocked, not in-memory). Run locally with the full stack up.

## GET /api/organizations

Lightweight authenticated read (baseline, minimal DB work) — 20 concurrent requests

- Concurrency: 20
- Total requests: 100
- Success rate: 100.0% (100/100)
- Latency: min 2.3ms, avg 11.5ms, p95 48.5ms, max 49.9ms

## GET /api/appointments/bloodbank/{orgId}/upcoming

Business-data read with a batched DonorProfile join + AgentStep lookup per row — measures real database response under concurrency

- Concurrency: 20
- Total requests: 100
- Success rate: 100.0% (100/100)
- Latency: min 5.0ms, avg 13.9ms, p95 32.1ms, max 38.3ms

## POST /api/internal/agent/search-donors

A single agent operation in isolation (Haversine distance + ranking over real donor rows) — the 'Agentic AI latency' for one tool call, not a full workflow

- Concurrency: 10
- Total requests: 50
- Success rate: 100.0% (50/50)
- Latency: min 2.7ms, avg 5.2ms, p95 7.3ms, max 14.2ms

## Full agent-workflow orchestration latency

`POST /api/requests` — creates a blood request and synchronously runs the Coordinator through stock_check -> search_donors -> validate_eligibility -> mark_awaiting_approval before returning. This is the real, end-to-end **Agentic AI latency** for one complete planning-and-delegation run, not a single tool call.

- Runs measured: 10
- Latency: min 37.5ms, avg 45.7ms, max 54.9ms
