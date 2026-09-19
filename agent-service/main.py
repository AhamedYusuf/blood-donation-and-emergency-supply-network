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


def get_backend_config():
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

    return backend_base_url, headers


@app.get("/health")
def health():
    return {"status": "ok"}


@app.post("/run-workflow")
def run_workflow(request: RunWorkflowRequest):
    backend_base_url, headers = get_backend_config()

    try:
        # 1. Create AgentWorkflow record
        workflow_response = requests.post(
            f"{backend_base_url}/api/internal/agent/workflows",
            json={
                "bloodRequestId": request.bloodRequestId
            },
            headers=headers,
            timeout=10
        )

        if workflow_response.status_code >= 400:
            raise HTTPException(
                status_code=workflow_response.status_code,
                detail=workflow_response.text
            )

        workflow = workflow_response.json()
        workflow_id = workflow["id"]

        # 2. Fetch BloodRequest data for the Coordinator
        blood_request_response = requests.get(
            (
                f"{backend_base_url}"
                f"/api/internal/agent/workflows/request/"
                f"{request.bloodRequestId}"
            ),
            headers=headers,
            timeout=10
        )

        if blood_request_response.status_code >= 400:
            raise HTTPException(
                status_code=blood_request_response.status_code,
                detail=blood_request_response.text
            )

        blood_request = blood_request_response.json()

        # 3. Validate fields required by downstream agents
        required_fields = [
            "bloodType",
            "unitsRequested",
            "urgency",
            "latitude",
            "longitude",
        ]

        missing_fields = [
            field
            for field in required_fields
            if blood_request.get(field) is None
        ]

        if missing_fields:
            raise HTTPException(
                status_code=400,
                detail=(
                    "Blood request is missing required fields: "
                    + ", ".join(missing_fields)
                )
            )

        # 4. Build complete CoordinatorState
        #
        # Important:
        # matching_dispatch_agent.search() expects location
        # as {"lat": ..., "lng": ...}
        initial_state = {
            "workflow_id": workflow_id,
            "blood_request_id": request.bloodRequestId,

            "blood_type": blood_request["bloodType"],
            "units_needed": blood_request["unitsRequested"],
            "urgency_level": blood_request["urgency"],

            "location": {
                "lat": blood_request["latitude"],
                "lng": blood_request["longitude"],
            },

            "current_step": "planning",

            "plan": [
                "stock_check",
                "search_donors",
                "validate_eligibility",
                "await_approval",
                "dispatch",
            ],
        }

        # 5. LangGraph thread configuration
        config = {
            "configurable": {
                "thread_id": workflow_id
            }
        }

        # 6. Start Coordinator Agent
        result = coordinator_graph.invoke(
            initial_state,
            config=config
        )

        return {
            "workflowId": workflow_id,
            "status": "started",
            "state": result,
        }

    except HTTPException:
        raise

    except requests.RequestException as exc:
        raise HTTPException(
            status_code=502,
            detail=f"Backend communication failed: {str(exc)}"
        )

    except Exception as exc:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to start workflow: {str(exc)}"
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
        "revise",
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
        snapshot = coordinator_graph.get_state(config)

        if not snapshot.values:
            raise HTTPException(
                status_code=404,
                detail="Workflow checkpoint not found."
            )

        result = coordinator_graph.invoke(
            Command(
                resume={
                    "decision": decision,
                    "comments": request.comments,
                }
            ),
            config=config
        )

        return {
            "workflowId": workflow_id,
            "decision": decision,
            "status": "resumed",
            "state": result,
        }

    except HTTPException:
        raise

    except Exception as exc:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to resume workflow: {str(exc)}"
        )