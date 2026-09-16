from __future__ import annotations

import json
from datetime import datetime, timezone
from typing import Any, TypedDict

from langgraph.checkpoint.memory import MemorySaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import interrupt

from agents.matching_dispatch_agent import dispatch as run_dispatch
from agents.matching_dispatch_agent import search as run_search
from shared.internal_client import InternalClient, InternalClientError


class CoordinatorState(TypedDict, total=False):
    # Workflow identity
    workflow_id: str
    blood_request_id: str

    # Request information
    blood_type: str
    units_needed: int
    urgency_level: str
    location: dict[str, float]

    # Coordinator state
    current_step: str
    plan: list[str]

    # Agent outputs
    stock_result: dict[str, Any]
    donor_search_result: dict[str, Any]
    eligibility_result: dict[str, Any]

    # Human decision
    approval_decision: str
    approval_comments: str | None

    # Final result
    dispatch_result: dict[str, Any]

    # Failure information
    error: str | None


def utc_now_iso() -> str:
    """
    Return an explicit UTC ISO timestamp.

    The Z suffix is important because PostgreSQL timestamp with time zone
    expects UTC values through Npgsql.
    """
    return (
        datetime.now(timezone.utc)
        .isoformat()
        .replace("+00:00", "Z")
    )


def log_workflow_step(
    state: CoordinatorState,
    *,
    agent_name: str,
    step_name: str,
    status: str,
    input_data: Any = None,
    output_data: Any = None,
    narrative: str | None = None,
    started_at: str | None = None,
    completed_at: str | None = None,
    error_message: str | None = None,
) -> None:
    """
    Save a coordinator-owned workflow step to the ASP.NET backend.

    Logging failure must not crash the main workflow.
    """
    workflow_id = state.get("workflow_id")

    if not workflow_id:
        print(
            f"[workflow-log] skipped {step_name}: "
            "workflow_id is missing"
        )
        return

    payload = {
        "agentName": agent_name,
        "stepName": step_name,
        "status": status,
        "inputJson": (
            json.dumps(input_data, default=str)
            if input_data is not None
            else None
        ),
        "outputJson": (
            json.dumps(output_data, default=str)
            if output_data is not None
            else None
        ),
        "narrative": narrative,
        "startedAt": started_at or utc_now_iso(),
        "completedAt": completed_at,
        "errorMessage": error_message,
    }

    try:
        InternalClient().post(
            (
                "/api/internal/agent/workflows/"
                f"{workflow_id}/steps"
            ),
            payload,
        )

    except InternalClientError as exc:
        print(
            f"[workflow-log] failed to log "
            f"{step_name}: {exc}"
        )


def stock_check_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Temporary Student 3 stub.

    Later this node will call Student 3's real stock-check agent.
    """
    started_at = utc_now_iso()

    result = {
        "sufficient": False,
        "message": "Temporary stock-check stub.",
    }

    log_workflow_step(
        state,
        agent_name="Stock Check Agent",
        step_name="stock_check",
        status="completed",
        input_data={
            "bloodType": state.get("blood_type"),
            "unitsNeeded": state.get("units_needed"),
            "location": state.get("location"),
        },
        output_data=result,
        narrative=(
            "Checked available blood stock. "
            "Temporary Student 3 stub is currently in use."
        ),
        started_at=started_at,
        completed_at=utc_now_iso(),
    )

    return {
        "current_step": "stock_check",
        "stock_result": result,
    }


def route_after_stock_check(
    state: CoordinatorState,
) -> str:
    """
    If enough stock exists, skip donor matching.

    Otherwise continue to donor search.
    """
    stock_result = state.get(
        "stock_result",
        {},
    )

    if stock_result.get("sufficient") is True:
        return "await_approval"

    return "search_donors"


def search_donors_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Student 4 Matching & Dispatch Agent - search mode.

    Student 4's search agent already writes its own AgentStep record,
    so the Coordinator must not log this step again.
    """
    search_input = {
        "workflowId": state.get("workflow_id"),
        "bloodType": state.get("blood_type"),
        "unitsNeeded": state.get("units_needed"),
        "location": state.get("location"),
        "urgencyLevel": state.get("urgency_level"),
    }

    try:
        result = run_search(search_input)

        return {
            "current_step": "search_donors",
            "donor_search_result": result,
        }

    except (InternalClientError, ValueError) as exc:
        error_message = (
            f"search_donors failed: {exc}"
        )

        return {
            "current_step": "search_donors",
            "donor_search_result": {
                "candidates": [],
            },
            "error": error_message,
        }


def validate_eligibility_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Temporary Student 1 stub.

    Later this node will call Student 1's real eligibility-validation
    agent.
    """
    started_at = utc_now_iso()

    eligibility_input = {
        "candidates": (
            state.get(
                "donor_search_result",
                {},
            ).get(
                "candidates",
                [],
            )
        ),
    }

    result = {
        "eligible": [],
        "excluded": [],
        "message": (
            "Temporary eligibility-validation stub."
        ),
    }

    log_workflow_step(
        state,
        agent_name="Eligibility Validation Agent",
        step_name="validate_eligibility",
        status="completed",
        input_data=eligibility_input,
        output_data=result,
        narrative=(
            "Validated donor eligibility. "
            "Temporary Student 1 stub is currently in use."
        ),
        started_at=started_at,
        completed_at=utc_now_iso(),
    )

    return {
        "current_step": "validate_eligibility",
        "eligibility_result": result,
    }


def await_approval_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Human-in-the-loop approval.

    The graph pauses until an authorized staff/admin user chooses:
    approve, reject, or revise.
    """
    started_at = utc_now_iso()

    decision = interrupt(
        {
            "workflowId": state.get(
                "workflow_id"
            ),
            "bloodRequestId": state.get(
                "blood_request_id"
            ),
            "message": (
                "Workflow requires authorized "
                "human approval."
            ),
            "allowedDecisions": [
                "approve",
                "reject",
                "revise",
            ],
        }
    )

    if not isinstance(decision, dict):
        raise ValueError(
            "Approval response must be an object."
        )

    approval_decision = str(
        decision.get(
            "decision",
            "",
        )
    ).lower()

    approval_comments = decision.get(
        "comments"
    )

    log_workflow_step(
        state,
        agent_name="Coordinator Agent",
        step_name="await_approval",
        status="completed",
        input_data={
            "allowedDecisions": [
                "approve",
                "reject",
                "revise",
            ]
        },
        output_data={
            "decision": approval_decision,
            "comments": approval_comments,
        },
        narrative=(
            "Human approval decision received: "
            f"{approval_decision}."
        ),
        started_at=started_at,
        completed_at=utc_now_iso(),
    )

    return {
        "current_step": "await_approval",
        "approval_decision": approval_decision,
        "approval_comments": approval_comments,
    }


def route_after_approval(
    state: CoordinatorState,
) -> str:
    """
    Approved workflows continue to dispatch.

    Reject and revise currently end this graph execution.
    Full revision-loop behavior will be improved separately.
    """
    decision = state.get(
        "approval_decision",
        "",
    ).lower()

    if decision == "approve":
        return "dispatch"

    return "end"


def dispatch_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Student 4 Matching & Dispatch Agent - dispatch mode.

    Student 4's dispatch agent owns its own logging, so the Coordinator
    must not create a duplicate dispatch AgentStep.
    """
    eligibility_result = state.get(
        "eligibility_result",
        {},
    )

    eligible = eligibility_result.get(
        "eligible",
        [],
    )

    dispatch_input = {
        "workflowId": state.get(
            "workflow_id"
        ),
        "eligible": eligible,
    }

    try:
        result = run_dispatch(
            dispatch_input
        )

        return {
            "current_step": "dispatch",
            "dispatch_result": result,
        }

    except (InternalClientError, ValueError) as exc:
        error_message = (
            f"dispatch failed: {exc}"
        )

        return {
            "current_step": "dispatch",
            "dispatch_result": {
                "status": "failed",
            },
            "error": error_message,
        }


def build_coordinator_graph() -> StateGraph:
    builder = StateGraph(
        CoordinatorState
    )

    builder.add_node(
        "stock_check",
        stock_check_node,
    )

    builder.add_node(
        "search_donors",
        search_donors_node,
    )

    builder.add_node(
        "validate_eligibility",
        validate_eligibility_node,
    )

    builder.add_node(
        "await_approval",
        await_approval_node,
    )

    builder.add_node(
        "dispatch",
        dispatch_node,
    )

    # Start workflow
    builder.add_edge(
        START,
        "stock_check",
    )

    # Stock routing
    builder.add_conditional_edges(
        "stock_check",
        route_after_stock_check,
        {
            "search_donors": "search_donors",
            "await_approval": "await_approval",
        },
    )

    # Donor search -> eligibility
    builder.add_edge(
        "search_donors",
        "validate_eligibility",
    )

    # Eligibility -> human approval
    builder.add_edge(
        "validate_eligibility",
        "await_approval",
    )

    # Human decision routing
    builder.add_conditional_edges(
        "await_approval",
        route_after_approval,
        {
            "dispatch": "dispatch",
            "end": END,
        },
    )

    # Dispatch ends this workflow execution
    builder.add_edge(
        "dispatch",
        END,
    )

    return builder


# In-memory LangGraph checkpoint storage.
#
# This allows interrupt() to pause and later resume the workflow
# while the Python process is still running.
memory = MemorySaver()

coordinator_graph = (
    build_coordinator_graph()
    .compile(
        checkpointer=memory
    )
)