"""
Stock Risk Agent.

Calls the protected .NET stock-risk endpoint and returns
the organization's current inventory risk information.
"""

from __future__ import annotations

from typing import Any
from uuid import UUID

from shared.internal_client import InternalClient, InternalClientError


AGENT_NAME = "stock_risk_agent"

STOCK_RISK_PATH = (
    "/api/internal/agent/stock-risk/{organization_id}"
)

_REQUIRED_RESPONSE_FIELDS = (
    "organizationId",
    "riskLevel",
    "totalUnits",
    "lowStockTypes",
    "atRiskInventory",
    "recommendation",
)


class StockRiskAgentError(ValueError):
    """Raised when Stock Risk Agent input is invalid."""


def run(
    payload: dict[str, Any],
    client: InternalClient | None = None,
) -> dict[str, Any]:

    print("\n" + "=" * 70)
    print("[STOCK RISK AGENT] START")
    print("=" * 70)

    print("[STOCK RISK AGENT] Input:")
    print(payload)

    # --------------------------------------------------
    # Validate input
    # --------------------------------------------------

    organization_id = _validate_input(payload)

    print(
        "[STOCK RISK AGENT] Organization:",
        organization_id,
    )

    # --------------------------------------------------
    # Create client
    # --------------------------------------------------

    client = client or InternalClient()

    # --------------------------------------------------
    # Backend endpoint
    # --------------------------------------------------

    path = STOCK_RISK_PATH.format(
        organization_id=organization_id
    )

    print(
        "[STOCK RISK AGENT] Calling:",
        path,
    )

    # --------------------------------------------------
    # Call backend
    # --------------------------------------------------

    try:

        result = client.get(path)

    except Exception as exc:

        print("\n" + "=" * 70)
        print("[STOCK RISK AGENT] BACKEND ERROR")
        print(str(exc))
        print("=" * 70)

        raise

    # --------------------------------------------------
    # Print backend response
    # --------------------------------------------------

    print("\n[STOCK RISK AGENT] Backend response:")
    print(result)

    # --------------------------------------------------
    # Validate response
    # --------------------------------------------------

    _validate_response(result)

    # --------------------------------------------------
    # Return result
    # --------------------------------------------------

    final_result = {
        **result,
        "current_agent": AGENT_NAME,
    }

    print("\n[STOCK RISK AGENT] Final result:")
    print(final_result)

    print("=" * 70)
    print("[STOCK RISK AGENT] COMPLETE")
    print("=" * 70)

    return final_result


def analyze_stock_risk(
    payload: dict[str, Any],
    client: InternalClient | None = None,
) -> dict[str, Any]:
    """Descriptive alias for the Stock Risk Agent."""

    return run(
        payload,
        client=client,
    )


def _validate_input(payload: Any) -> str:

    if not isinstance(payload, dict):
        raise StockRiskAgentError(
            "payload must be a dict"
        )

    raw = payload.get("organizationId")

    if raw in (None, ""):
        raise StockRiskAgentError(
            "missing required field(s): organizationId"
        )

    if not isinstance(raw, str):
        raise StockRiskAgentError(
            "'organizationId' must be a string UUID"
        )

    try:

        return str(UUID(raw))

    except ValueError as exc:

        raise StockRiskAgentError(
            "'organizationId' must be a valid UUID"
        ) from exc


def _validate_response(result: Any) -> None:

    if not isinstance(result, dict):
        raise InternalClientError(
            "stock-risk response must be a JSON object"
        )

    # --------------------------------------------------
    # Check required fields
    # --------------------------------------------------

    missing = [
        key
        for key in _REQUIRED_RESPONSE_FIELDS
        if key not in result
    ]

    if missing:

        raise InternalClientError(
            "stock-risk response is missing "
            "required field(s): "
            + ", ".join(missing)
        )

    # --------------------------------------------------
    # Validate risk level
    # --------------------------------------------------

    if not isinstance(
        result["riskLevel"],
        str,
    ):

        raise InternalClientError(
            "stock-risk response field "
            "'riskLevel' must be a string"
        )

    # --------------------------------------------------
    # Validate numeric fields
    # --------------------------------------------------

    for field in (
        "totalUnits",
        "lowStockTypes",
    ):

        value = result[field]

        if (
            not isinstance(value, int)
            or isinstance(value, bool)
            or value < 0
        ):

            raise InternalClientError(
                f"stock-risk response field "
                f"'{field}' must be a "
                "non-negative integer"
            )

    # --------------------------------------------------
    # Validate at-risk inventory
    # --------------------------------------------------

    if not isinstance(
        result["atRiskInventory"],
        list,
    ):

        raise InternalClientError(
            "stock-risk response field "
            "'atRiskInventory' must be a list"
        )

    # --------------------------------------------------
    # Validate recommendation
    # --------------------------------------------------

    if not isinstance(
        result["recommendation"],
        str,
    ):

        raise InternalClientError(
            "stock-risk response field "
            "'recommendation' must be a string"
        )