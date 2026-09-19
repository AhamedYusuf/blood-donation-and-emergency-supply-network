from __future__ import annotations

import json
from datetime import datetime, timezone
from typing import Any, TypedDict

from langgraph.checkpoint.memory import MemorySaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import interrupt

from agents.eligibility_validation_agent import run as run_eligibility
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
    Student 1 Eligibility Validation Agent.

    Student 4's search agent returns candidate donor objects. The
    eligibility agent expects donor IDs plus the required blood type,
    so the Coordinator adapts the data between both agents.

    Student 1's endpoint owns its AgentStep logging, so the Coordinator
    does not create a duplicate eligibility step.
    """
    donor_search_result = state.get(
        "donor_search_result",
        {},
    )

    candidates = donor_search_result.get(
        "candidates",
        [],
    )

    candidate_donor_ids = [
        candidate.get("donorId")
        for candidate in candidates
        if isinstance(candidate, dict)
        and candidate.get("donorId")
    ]

    eligibility_input = {
        "workflow_id": state.get("workflow_id"),
        "candidate_donor_ids": candidate_donor_ids,
        "required_blood_type": state.get("blood_type"),
    }

    result = run_eligibility(
        eligibility_input
    )

    error = result.get("error")

    if error:
        return {
            "current_step": "validate_eligibility",
            "eligibility_result": {
                "eligible": [],
                "excluded": result.get(
                    "excluded_donors",
                    [],
                ),
            },
            "error": (
                "validate_eligibility failed: "
                f"{error}"
            ),
        }

    eligible_ids = {
        str(donor_id)
        for donor_id in result.get(
            "eligible_donor_ids",
            [],
        )
    }

    eligible = [
        {
            "donorId": candidate.get("donorId")
        }
        for candidate in candidates
        if isinstance(candidate, dict)
        and candidate.get("donorId") is not None
        and str(candidate.get("donorId")) in eligible_ids
    ]

    eligibility_result = {
        "eligible": eligible,
        "excluded": result.get(
            "excluded_donors",
            [],
        ),
    }

    return {
        "current_step": "validate_eligibility",
        "eligibility_result": eligibility_result,
    }


def mark_awaiting_approval_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Persist awaiting_approval before LangGraph pauses.
    """
    workflow_id = state.get("workflow_id")

    if not workflow_id:
        raise ValueError(
            "workflow_id is required before awaiting approval."
        )

    try:
        InternalClient().post(
            (
                "/api/internal/agent/workflows/"
                f"{workflow_id}/status"
            ),
            {
                "status": "awaiting_approval",
                "failureReason": None,
            },
        )

    except InternalClientError as exc:
        raise ValueError(
            "Failed to mark workflow as "
            f"awaiting approval: {exc}"
        ) from exc

    return {
        "current_step": "awaiting_approval",
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


def revision_replan_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Re-plan the workflow after a human requests a revision.

    The backend already increments RevisionCount and enforces
    the maximum revision limit.
    """
    started_at = utc_now_iso()

    comments = state.get(
        "approval_comments"
    )

    revised_plan = [
        "stock_check",
        "search_donors",
        "validate_eligibility",
        "await_approval",
        "dispatch",
    ]

    log_workflow_step(
        state,
        agent_name="Coordinator Agent",
        step_name="revision_replan",
        status="completed",
        input_data={
            "decision": "revise",
            "comments": comments,
        },
        output_data={
            "plan": revised_plan,
        },
        narrative=(
            "Human revision requested. "
            "Coordinator restarted the workflow "
            "using the provided feedback."
        ),
        started_at=started_at,
        completed_at=utc_now_iso(),
    )

    return {
        "current_step": "revision_replan",
        "plan": revised_plan,

        # Clear previous-cycle results.
        "stock_result": {},
        "donor_search_result": {},
        "eligibility_result": {},
        "dispatch_result": {},

        # Next approval interrupt will populate these again.
        "approval_decision": "",
        "error": None,
    }


def route_after_approval(
    state: CoordinatorState,
) -> str:
    """
    Route according to the human decision.

    approve -> dispatch
    revise  -> re-plan and run workflow again
    reject  -> end
    """
    decision = state.get(
        "approval_decision",
        "",
    ).lower()

    if decision == "approve":
        return "dispatch"

    if decision == "revise":
        return "revision_replan"

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

            # Clear any old error because dispatch succeeded.
            "error": None,
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


def finalize_workflow_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Persist the final workflow status after dispatch.

    Successful dispatch -> completed
    Failed dispatch     -> failed
    """
    workflow_id = state.get(
        "workflow_id"
    )

    if not workflow_id:
        raise ValueError(
            "workflow_id is required to finalize workflow."
        )

    dispatch_result = state.get(
        "dispatch_result",
        {},
    )

    dispatch_failed = (
        dispatch_result.get("status") == "failed"
    )

    if dispatch_failed:
        final_status = "failed"
        failure_reason = (
            state.get("error")
            or "Dispatch failed."
        )
    else:
        final_status = "completed"
        failure_reason = None

    try:
        InternalClient().post(
            (
                "/api/internal/agent/workflows/"
                f"{workflow_id}/status"
            ),
            {
                "status": final_status,
                "failureReason": failure_reason,
            },
        )

    except InternalClientError as exc:
        raise ValueError(
            "Failed to persist final workflow "
            f"status: {exc}"
        ) from exc

    log_workflow_step(
        state,
        agent_name="Coordinator Agent",
        step_name="finalize_workflow",
        status="completed",
        input_data={
            "dispatchResult": dispatch_result,
        },
        output_data={
            "workflowStatus": final_status,
            "failureReason": failure_reason,
        },
        narrative=(
            f"Workflow finalized with status "
            f"'{final_status}'."
        ),
        completed_at=utc_now_iso(),
        error_message=failure_reason,
    )

    return {
        "current_step": "finalize_workflow",
        "error": (
            failure_reason
            if dispatch_failed
            else None
        ),
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
        "mark_awaiting_approval",
        mark_awaiting_approval_node,
    )

    builder.add_node(
        "await_approval",
        await_approval_node,
    )

    builder.add_node(
        "revision_replan",
        revision_replan_node,
    )

    builder.add_node(
        "dispatch",
        dispatch_node,
    )

    builder.add_node(
        "finalize_workflow",
        finalize_workflow_node,
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
            "await_approval": "mark_awaiting_approval",
        },
    )

    # Donor search -> eligibility
    builder.add_edge(
        "search_donors",
        "validate_eligibility",
    )

    # Eligibility -> persist awaiting approval
    builder.add_edge(
        "validate_eligibility",
        "mark_awaiting_approval",
    )

    # Persist status -> human approval interrupt
    builder.add_edge(
        "mark_awaiting_approval",
        "await_approval",
    )

    # Human decision routing
    builder.add_conditional_edges(
        "await_approval",
        route_after_approval,
        {
            "dispatch": "dispatch",
            "revision_replan": "revision_replan",
            "end": END,
        },
    )

    # Revise -> re-run workflow
    builder.add_edge(
        "revision_replan",
        "stock_check",
    )

    # Dispatch -> final DB status
    builder.add_edge(
        "dispatch",
        "finalize_workflow",
    )

    # Final status -> end
    builder.add_edge(
        "finalize_workflow",
        END,
    )

    return builder


# In-memory LangGraph checkpoint storage.
memory = MemorySaver()

coordinator_graph = (
    build_coordinator_graph()
    .compile(
        checkpointer=memory
    )
)