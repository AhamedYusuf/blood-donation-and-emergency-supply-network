import os
import traceback
import requests

from typing import Optional

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from langgraph.types import Command

from agents.coordinator_agent import coordinator_graph
from agents.stock_check_agent import run as run_stock_check
from agents.stock_risk_agent import run as run_stock_risk
from agents.emergency_recommendation_agent import (
    run as run_emergency_recommendation,
)


# =========================================================
# FASTAPI APP
# =========================================================

app = FastAPI(
    title="Blood Donation Network Agent Service"
)


# =========================================================
# CORS
# =========================================================

app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:5173",
        "http://127.0.0.1:5173",
    ],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# =========================================================
# REQUEST MODELS
# =========================================================

class StockCheckAgentRequest(BaseModel):
    workflowId: str
    requestingOrgId: str
    bloodType: str
    unitsNeeded: int


class StockRiskAgentRequest(BaseModel):
    organizationId: str


class EmergencyRecommendationAgentRequest(BaseModel):
    bloodType: str
    requiredUnits: int
    urgency: str


class RunWorkflowRequest(BaseModel):
    bloodRequestId: str


class ResumeWorkflowRequest(BaseModel):
    decision: str
    comments: Optional[str] = None


# =========================================================
# STOCK CHECK AGENT
# =========================================================

@app.post("/agents/stock-check")
def agent_stock_check(
    request: StockCheckAgentRequest,
):
    print("\n" + "=" * 70)
    print("[MAIN] STOCK CHECK REQUEST")
    print(request.model_dump())
    print("=" * 70)

    try:

        agent_request = {
            "workflowId": request.workflowId,
            "requestingOrgId": request.requestingOrgId,
            "bloodType": request.bloodType,
            "unitsNeeded": request.unitsNeeded,
        }

        print("\n[MAIN] SENDING TO STOCK CHECK AGENT")
        print(agent_request)

        result = run_stock_check(
            agent_request
        )

        print("\n" + "=" * 70)
        print("[MAIN] STOCK CHECK RESULT")
        print(result)
        print("=" * 70)

        return result

    except Exception as exc:

        print("\n" + "=" * 70)
        print("[MAIN] STOCK CHECK ERROR")
        print(str(exc))
        print("=" * 70)

        traceback.print_exc()

        raise HTTPException(
            status_code=500,
            detail=(
                f"Stock Check Agent failed: {str(exc)}"
            ),
        )


# =========================================================
# STOCK RISK AGENT
# =========================================================

@app.post("/agents/stock-risk")
def agent_stock_risk(
    request: StockRiskAgentRequest,
):
    print("\n" + "=" * 70)
    print("[MAIN] STOCK RISK REQUEST")
    print(request.model_dump())
    print("=" * 70)

    try:

        result = run_stock_risk(
            {
                "organizationId": request.organizationId,
            }
        )

        print("\n[MAIN] STOCK RISK RESULT")
        print(result)

        return result

    except Exception as exc:

        print("\n" + "=" * 70)
        print("[MAIN] STOCK RISK ERROR")
        print(str(exc))
        print("=" * 70)

        traceback.print_exc()

        raise HTTPException(
            status_code=500,
            detail=(
                f"Stock Risk Agent failed: {str(exc)}"
            ),
        )


# =========================================================
# EMERGENCY RECOMMENDATION AGENT
# =========================================================

@app.post("/agents/emergency-recommendation")
def agent_emergency_recommendation(
    request: EmergencyRecommendationAgentRequest,
):
    print("\n" + "=" * 70)
    print("[MAIN] EMERGENCY RECOMMENDATION REQUEST")
    print(request.model_dump())
    print("=" * 70)

    try:

        result = run_emergency_recommendation(
            {
                "bloodType": request.bloodType,
                "requiredUnits": request.requiredUnits,
                "urgency": request.urgency,
            }
        )

        print("\n[MAIN] EMERGENCY RECOMMENDATION RESULT")
        print(result)

        return result

    except Exception as exc:

        print("\n" + "=" * 70)
        print("[MAIN] EMERGENCY RECOMMENDATION ERROR")
        print(str(exc))
        print("=" * 70)

        traceback.print_exc()

        raise HTTPException(
            status_code=500,
            detail=(
                "Emergency Recommendation Agent failed: "
                f"{str(exc)}"
            ),
        )


# =========================================================
# BACKEND CONFIGURATION
# =========================================================

def get_backend_config():

    backend_base_url = os.getenv(
        "BACKEND_BASE_URL",
        "http://localhost:5067",
    )

    internal_secret = os.getenv(
        "INTERNAL_AGENT_SECRET"
    )

    if not internal_secret:
        raise HTTPException(
            status_code=500,
            detail=(
                "INTERNAL_AGENT_SECRET is not configured."
            ),
        )

    headers = {
        "X-Internal-Secret": internal_secret,
    }

    return backend_base_url, headers


# =========================================================
# HEALTH CHECK
# =========================================================

@app.get("/health")
def health():
    return {
        "status": "ok"
    }


# =========================================================
# RUN WORKFLOW
# =========================================================

@app.post("/run-workflow")
def run_workflow(
    request: RunWorkflowRequest,
):

    backend_base_url, headers = (
        get_backend_config()
    )

    try:

        # -------------------------------------------------
        # 1. Create AgentWorkflow record
        # -------------------------------------------------

        workflow_response = requests.post(
            (
                f"{backend_base_url}"
                "/api/internal/agent/workflows"
            ),
            json={
                "bloodRequestId":
                    request.bloodRequestId
            },
            headers=headers,
            timeout=10,
        )

        if workflow_response.status_code >= 400:
            raise HTTPException(
                status_code=workflow_response.status_code,
                detail=workflow_response.text,
            )

        workflow = workflow_response.json()

        workflow_id = workflow["id"]


        # -------------------------------------------------
        # 2. Fetch BloodRequest data
        # -------------------------------------------------

        blood_request_response = requests.get(
            (
                f"{backend_base_url}"
                "/api/internal/agent/workflows/request/"
                f"{request.bloodRequestId}"
            ),
            headers=headers,
            timeout=10,
        )

        if blood_request_response.status_code >= 400:
            raise HTTPException(
                status_code=(
                    blood_request_response.status_code
                ),
                detail=(
                    blood_request_response.text
                ),
            )

        blood_request = (
            blood_request_response.json()
        )


        # -------------------------------------------------
        # 3. Validate required fields
        # -------------------------------------------------

        required_fields = [
            "organizationId",
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
                    "Blood request is missing "
                    "required fields: "
                    + ", ".join(missing_fields)
                ),
            )


        # -------------------------------------------------
        # 4. Build Coordinator State
        # -------------------------------------------------

        initial_state = {

            "workflow_id":
                workflow_id,

            "blood_request_id":
                request.bloodRequestId,

            "organization_id":
                blood_request["organizationId"],

            "requesting_org_id":
                blood_request["organizationId"],

            "blood_type":
                blood_request["bloodType"],

            "units_needed":
                blood_request["unitsRequested"],

            "urgency_level":
                blood_request["urgency"],

            "location": {
                "lat":
                    blood_request["latitude"],

                "lng":
                    blood_request["longitude"],
            },

            "current_step":
                "planning",

            "plan": [
                "stock_check",
                "search_donors",
                "validate_eligibility",
                "await_approval",
                "dispatch",
            ],
        }


        # -------------------------------------------------
        # 5. LangGraph configuration
        # -------------------------------------------------

        config = {
            "configurable": {
                "thread_id":
                    workflow_id
            }
        }


        # -------------------------------------------------
        # 6. Start Coordinator Agent
        # -------------------------------------------------

        result = coordinator_graph.invoke(
            initial_state,
            config=config,
        )


        return {
            "workflowId":
                workflow_id,

            "status":
                "started",

            "state":
                result,
        }


    except HTTPException:
        raise


    except requests.RequestException as exc:

        raise HTTPException(
            status_code=502,
            detail=(
                "Backend communication failed: "
                f"{str(exc)}"
            ),
        )


    except Exception as exc:

        traceback.print_exc()

        raise HTTPException(
            status_code=500,
            detail=(
                "Failed to start workflow: "
                f"{str(exc)}"
            ),
        )


# =========================================================
# RESUME WORKFLOW
# =========================================================

@app.post(
    "/resume-workflow/{workflow_id}"
)
def resume_workflow(
    workflow_id: str,
    request: ResumeWorkflowRequest,
):

    decision = (
        request.decision
        .lower()
        .strip()
    )


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
            ),
        )


    config = {
        "configurable": {
            "thread_id":
                workflow_id
        }
    }


    try:

        # -------------------------------------------------
        # Get existing workflow state
        # -------------------------------------------------

        snapshot = (
            coordinator_graph.get_state(
                config
            )
        )


        if not snapshot.values:

            raise HTTPException(
                status_code=404,
                detail=(
                    "Workflow checkpoint "
                    "not found."
                ),
            )


        # -------------------------------------------------
        # Resume workflow
        # -------------------------------------------------

        result = coordinator_graph.invoke(
            Command(
                resume={
                    "decision":
                        decision,

                    "comments":
                        request.comments,
                }
            ),
            config=config,
        )


        return {
            "workflowId":
                workflow_id,

            "decision":
                decision,

            "status":
                "resumed",

            "state":
                result,
        }


    except HTTPException:
        raise


    except Exception as exc:

        traceback.print_exc()

        raise HTTPException(
            status_code=500,
            detail=(
                "Failed to resume workflow: "
                f"{str(exc)}"
            ),
        )