"""Stock-check agent contract with the ASP.NET Core backend."""

from __future__ import annotations

from typing import Any

from shared.internal_client import InternalClient, InternalClientError


CHECK_STOCK_PATH = "/api/internal/agent/check-stock"
VALID_BLOOD_TYPES = {
    "A+",
    "A-",
    "B+",
    "B-",
    "AB+",
    "AB-",
    "O+",
    "O-",
}


class StockCheckAgentError(ValueError):
    """Raised when the Coordinator sends an invalid stock-check request."""


def _require_non_empty_string(
    request: dict[str, Any],
    field: str,
) -> str:
    value = request.get(field)
    if not isinstance(value, str) or not value.strip():
        raise StockCheckAgentError(f"{field} is required")
    return value.strip()


def _validate_response(result: dict[str, Any]) -> None:
    required_fields = {
        "sufficient",
        "ownStockUnits",
        "shortfallUnits",
        "candidateTransferOrgs",
    }

    if not isinstance(result, dict):
        raise InternalClientError(
            "check-stock returned an unexpected response type"
        )

    missing = required_fields.difference(result)
    if missing:
        raise InternalClientError(
            "check-stock response is missing fields: "
            + ", ".join(sorted(missing))
        )

    if not isinstance(result["sufficient"], bool):
        raise InternalClientError(
            "check-stock response field 'sufficient' must be boolean"
        )

    for field in ("ownStockUnits", "shortfallUnits"):
        value = result[field]
        if isinstance(value, bool) or not isinstance(value, int) or value < 0:
            raise InternalClientError(
                f"check-stock response field '{field}' must be a non-negative integer"
            )

    candidates = result["candidateTransferOrgs"]
    if not isinstance(candidates, list):
        raise InternalClientError(
            "check-stock response field 'candidateTransferOrgs' must be a list"
        )

    for candidate in candidates:
        if not isinstance(candidate, dict):
            raise InternalClientError(
                "candidateTransferOrgs entries must be objects"
            )
        for field in ("organizationId", "distanceKm", "unitsAvailable"):
            if field not in candidate:
                raise InternalClientError(
                    f"candidateTransferOrgs entry is missing '{field}'"
                )


def run(
    request: dict[str, Any],
    *,
    client: InternalClient | None = None,
) -> dict[str, Any]:
    """Validate the request and call the backend stock-check endpoint."""
    workflow_id = _require_non_empty_string(request, "workflowId")
    requesting_org_id = _require_non_empty_string(request, "requestingOrgId")

    blood_type = request.get("bloodType")
    if not isinstance(blood_type, str) or blood_type not in VALID_BLOOD_TYPES:
        raise StockCheckAgentError(
            "bloodType must be one of: "
            + ", ".join(sorted(VALID_BLOOD_TYPES))
        )

    units_needed = request.get("unitsNeeded")
    if (
        isinstance(units_needed, bool)
        or not isinstance(units_needed, int)
        or units_needed <= 0
    ):
        raise StockCheckAgentError("unitsNeeded must be a positive integer")

    payload = {
        "workflowId": workflow_id,
        "requestingOrgId": requesting_org_id,
        "bloodType": blood_type,
        "unitsNeeded": units_needed,
    }

    result = (client or InternalClient()).post(CHECK_STOCK_PATH, payload)
    _validate_response(result)
    return {
        **result,
        "current_agent": "stock_check_agent",
    }