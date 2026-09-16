import os

import requests
from fastapi import FastAPI, HTTPException
from langgraph.types import Command
from pydantic import BaseModel

from agents.coordinator_agent import coordinator_graph


app = FastAPI(title="Blood Donation Network Agent Service")


class RunWorkflowRequest(BaseModel):
    bloodRequestId: str


class ResumeWorkflowRequest(BaseModel):
    decision: str
    comments: str | None = None


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/run-workflow")
def run_workflow(request: RunWorkflowRequest):
    backend_base_url = os.getenv(
        "BACKEND_BASE_URL",
        "http://localhost:5067"
    )

    internal_secret = os.getenv("INTERNAL_AGENT_SECRET")

    if not internal_secret:
        raise HTTPException(
            status_code=500,
            detail="INTERNAL_AGENT_SECRET is not configured."
        )

    headers = {
        "X-Internal-Secret": internal_secret
    }

    try:
        # 1. Create workflow record through ASP.NET backend
        response = requests.post(
            f"{backend_base_url}/api/internal/agent/workflows",
            json={
                "bloodRequestId": request.bloodRequestId
            },
            headers=headers,
            timeout=10
        )

        if response.status_code >= 400:
            raise HTTPException(
                status_code=response.status_code,
                detail=response.text
            )

        workflow = response.json()
        workflow_id = workflow["id"]

        # 2. Initial coordinator state
        initial_state = {
            "workflow_id": workflow_id,
            "blood_request_id": request.bloodRequestId,
            "current_step": "planning",
            "plan": [
                "stock_check",
                "search_donors",
                "validate_eligibility",
                "await_approval",
                "dispatch"
            ]
        }

        # 3. LangGraph thread configuration
        config = {
            "configurable": {
                "thread_id": workflow_id
            }
        }

        # 4. Start coordinator workflow
        result = coordinator_graph.invoke(
            initial_state,
            config=config
        )

        return {
            "workflowId": workflow_id,
            "status": "started",
            "state": result
        }

    except requests.RequestException as exc:
        raise HTTPException(
            status_code=502,
            detail=f"Backend communication failed: {str(exc)}"
        )


@app.post("/resume-workflow/{workflow_id}")
def resume_workflow(
    workflow_id: str,
    request: ResumeWorkflowRequest
):
    decision = request.decision.lower().strip()

    allowed_decisions = {
        "approve",
        "reject",
        "revise"
    }

    if decision not in allowed_decisions:
        raise HTTPException(
            status_code=400,
            detail=(
                "Invalid decision. "
                "Use approve, reject, or revise."
            )
        )

    config = {
        "configurable": {
            "thread_id": workflow_id
        }
    }

    try:
        # Check whether this workflow checkpoint exists
        snapshot = coordinator_graph.get_state(config)

        if not snapshot.values:
            raise HTTPException(
                status_code=404,
                detail="Workflow checkpoint not found."
            )

        # Resume the interrupt() inside await_approval_node
        result = coordinator_graph.invoke(
            Command(
                resume={
                    "decision": decision,
                    "comments": request.comments
                }
            ),
            config=config
        )

        return {
            "workflowId": workflow_id,
            "decision": decision,
            "status": "resumed",
            "state": result
        }

    except HTTPException:
        raise

    except Exception as exc:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to resume workflow: {str(exc)}"
        )