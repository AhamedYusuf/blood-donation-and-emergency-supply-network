# Agentic AI Evaluation — Golden Cases

Assignment 1 §12 requires "evidence that at least one complete minimum
acceptance workflow passes a suitable golden case, including correct
planning and delegation, agent and tool selection, structured outputs,
deterministic validation, business-rule compliance, approval enforcement,
prompt-injection resistance, failure recovery and safe failure," and is
explicit that **LLM-as-a-judge must not be the only evaluation method.**

This harness uses only rule-based assertions against the real system's
actual execution trace — no LLM judges its own output. That's possible
because the Coordinator's planning/routing/validation logic is
deterministic Python (the local LLM is only ever used to generate
human-readable narrative text, never to make decisions), so every golden
case's expected outcome is a concrete, checkable fact rather than a
subjective judgement.

## Prerequisites

Same as `testing/performance/README.md` — the full stack (PostgreSQL,
backend on `:5067`, agent-service on `:8000`) running locally with the
seeded dev accounts (`staff1@example.com` / `admin@blooddonation.local`).

## Run

```bash
cd testing/agent-evaluation
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
python3 run_golden_cases.py
```

Each run creates its own fresh, isolated donors and requests (unique
emails per run), so it's safe to re-run repeatedly without conflicting
with previous runs or shared dev data.

## What each golden case actually proves

| Case | Spec requirement it evidences |
|---|---|
| 1 | Correct planning and delegation, agent/tool selection, approval enforcement, auditable result |
| 2 | Safe failure — a hard-to-match request reaches a defined state, never crashes or hangs silently |
| 3 | Approval enforcement — the required dispatch guard rejects an unapproved workflow (409) |
| 4 | Prompt-injection resistance — adversarial text in a free-text field has zero effect on routing/approval |
| 5 / 5b | Failure recovery and deterministic validation — the revise loop re-plans correctly, and the 3-revision cap is genuinely enforced (verified precisely: the 4th attempt transitions the workflow to `failed` with the count held at 3, not silently ignored or looping) |
| 6 | Business-rule compliance — Eligibility excludes a real, blood-type-compatible but medically ineligible donor (`recent_illness`), proving it does real domain validation, not just a blood-type pass-through |

One disclosed limitation, found while building Case 5b and left in the
report rather than hidden: the 3-revision cap is enforced by the .NET
`/revise` endpoint's own counter, not inside the LangGraph state machine
itself — a direct call to the agent-service's own `/resume-workflow`
would bypass it (see the whole-system bug audit for the related finding
about that endpoint having no inbound authentication either).

Each run writes `agent_evaluation_report.md` with the real pass/fail
results and evidence, ready to paste into the assignment's Agentic AI
Evaluation Report section.
