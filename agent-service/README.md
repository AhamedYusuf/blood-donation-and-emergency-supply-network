# Agent Service

Python service that runs the Agentic AI pipeline for the Blood Donation &
Emergency Supply Network. The agents call back into the ASP.NET Core
backend's `/api/internal/*` endpoints; they are never called directly by
the React or Flutter apps.

## Layout

```
agent-service/
  main.py                         FastAPI app (health check; /run-workflow is Student 2)
  requirements.txt
  agents/
    coordinator_agent.py          Student 2  — LangGraph graph + orchestration
    stock_check_agent.py          Student 3
    matching_dispatch_agent.py    Student 4  — search / dispatch nodes
    eligibility_validation_agent.py  Student 1
  shared/
    internal_client.py            calls the backend's /api/internal/* endpoints
    ollama_client.py              Student 2  — local LLM narrative text
  tests/
```

## Local setup

```bash
cd agent-service
python3 -m venv .venv
source .venv/bin/activate            # Windows: .venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env                 # then edit if your backend isn't on :5067
```

## Run

```bash
uvicorn main:app --reload --port 8000
# health check:
curl http://localhost:8000/health
```

Start order for the full stack: PostgreSQL → ASP.NET Core API → this
service → React.

## Tests

```bash
cd agent-service
source .venv/bin/activate
pytest
```

`tests/test_internal_client.py` and `tests/test_matching_dispatch_agent.py`
monkeypatch the HTTP layer, so no running backend is required.

## Environment variables

| Variable | Purpose | Default |
|---|---|---|
| `BACKEND_BASE_URL` | Base URL of the ASP.NET Core API | `http://localhost:5067` |
| `INTERNAL_AGENT_SECRET` | Shared secret for `/api/internal/*`; must match the backend | _(required)_ |
