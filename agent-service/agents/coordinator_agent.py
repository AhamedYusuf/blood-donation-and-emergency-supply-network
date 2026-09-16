from __future__ import annotations

from typing import Any, TypedDict

from langgraph.checkpoint.memory import MemorySaver
from langgraph.graph import END, START, StateGraph
from langgraph.types import interrupt


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


def stock_check_node(state: CoordinatorState) -> dict[str, Any]:
    """
    Temporary Student 3 stub.

    Later this node will call the real stock-check agent/internal endpoint.
    For now we return insufficient stock so the complete graph path can
    continue through donor search and eligibility validation.
    """
    return {
        "current_step": "stock_check",
        "stock_result": {
            "sufficient": False,
            "message": "Temporary stock-check stub.",
        },
    }


def route_after_stock_check(state: CoordinatorState) -> str:
    """
    If enough stock already exists, donor matching can be skipped.
    Otherwise continue to donor search.
    """
    stock_result = state.get("stock_result", {})

    if stock_result.get("sufficient") is True:
        return "await_approval"

    return "search_donors"


def search_donors_node(state: CoordinatorState) -> dict[str, Any]:
    """
    Temporary Student 4 stub.

    Later this node will call the real donor-search logic.
    """
    return {
        "current_step": "search_donors",
        "donor_search_result": {
            "candidates": [],
            "message": "Temporary donor-search stub.",
        },
    }


def validate_eligibility_node(
    state: CoordinatorState,
) -> dict[str, Any]:
    """
    Temporary Student 1 stub.

    Later this node will call the real eligibility-validation logic.
    """
    return {
        "current_step": "validate_eligibility",
        "eligibility_result": {
            "eligible": [],
            "excluded": [],
            "message": "Temporary eligibility-validation stub.",
        },
    }


def await_approval_node(state: CoordinatorState) -> dict[str, Any]:
    """
    Human-in-the-loop interrupt.

    The graph pauses here until an authorized staff/admin user chooses:
    approve, reject, or revise.
    """
    decision = interrupt(
        {
            "workflowId": state.get("workflow_id"),
            "bloodRequestId": state.get("blood_request_id"),
            "message": "Workflow requires authorized human approval.",
            "allowedDecisions": [
                "approve",
                "reject",
                "revise",
            ],
        }
    )

    if not isinstance(decision, dict):
        raise ValueError("Approval response must be an object.")

    return {
        "current_step": "await_approval",
        "approval_decision": str(
            decision.get("decision", "")
        ).lower(),
        "approval_comments": decision.get("comments"),
    }


def route_after_approval(state: CoordinatorState) -> str:
    """
    Approved workflows continue to dispatch.

    Reject/revise currently end this graph execution.
    Resume/revision behaviour will be connected in the next step.
    """
    decision = state.get("approval_decision", "").lower()

    if decision == "approve":
        return "dispatch"

    return "end"


def dispatch_node(state: CoordinatorState) -> dict[str, Any]:
    """
    Temporary Student 4 dispatch stub.

    Later this node will call the real dispatch implementation.
    """
    return {
        "current_step": "dispatch",
        "dispatch_result": {
            "status": "pending_real_dispatch_integration",
            "message": "Temporary dispatch stub.",
        },
    }


def build_coordinator_graph() -> StateGraph:
    builder = StateGraph(CoordinatorState)

    builder.add_node("stock_check", stock_check_node)
    builder.add_node("search_donors", search_donors_node)
    builder.add_node(
        "validate_eligibility",
        validate_eligibility_node,
    )
    builder.add_node("await_approval", await_approval_node)
    builder.add_node("dispatch", dispatch_node)

    builder.add_edge(START, "stock_check")

    builder.add_conditional_edges(
        "stock_check",
        route_after_stock_check,
        {
            "search_donors": "search_donors",
            "await_approval": "await_approval",
        },
    )

    builder.add_edge(
        "search_donors",
        "validate_eligibility",
    )

    builder.add_edge(
        "validate_eligibility",
        "await_approval",
    )

    builder.add_conditional_edges(
        "await_approval",
        route_after_approval,
        {
            "dispatch": "dispatch",
            "end": END,
        },
    )

    builder.add_edge("dispatch", END)

    return builder


# In-memory LangGraph checkpoint storage.
# This allows the workflow to pause at interrupt() and be resumed later
# while the Python service is still running.
memory = MemorySaver()

coordinator_graph = build_coordinator_graph().compile(
    checkpointer=memory
)