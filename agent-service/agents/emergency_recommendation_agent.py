"""Student 3 Emergency Recommendation Agent.

The Python node validates a string-based agent contract, maps the request
values to the backend's enum representation, calls the internal inventory
recommendation endpoint, and normalizes the backend response into the
public agent contract.
"""

from __future__ import annotations

from typing import Any

from shared.internal_client import InternalClient, InternalClientError

AGENT_NAME = "emergency_recommendation_agent"
RECOMMENDATION_PATH = "/api/internal/agent/inventory-recommendation"

BLOOD_TYPE_TO_NUMBER = {
    "A+": 0,
    "A-": 1,
    "B+": 2,
    "B-": 3,
    "AB+": 4,
    "AB-": 5,
    "O+": 6,
    "O-": 7,
}

NUMBER_TO_BLOOD_TYPE = {
    value: key
    for key, value in BLOOD_TYPE_TO_NUMBER.items()
}

URGENCY_TO_NUMBER = {
    "Normal": 0,
    "Urgent": 1,
    "Critical": 2,
}

NUMBER_TO_URGENCY = {
    value: key
    for key, value in URGENCY_TO_NUMBER.items()
}

_REQUIRED_FIELDS = (
    "bloodType",
    "requiredUnits",
    "urgency",
)


class EmergencyRecommendationAgentError(ValueError):
    """Raised when the Emergency Recommendation Agent contract is invalid."""


def run(
    payload: dict[str, Any],
    client: InternalClient | None = None,
) -> dict[str, Any]:
    """Run the Emergency Recommendation Agent.

    Expected input:

        {
            "bloodType": "O-",
            "requiredUnits": 10,
            "urgency": "Critical"
        }

    Returns:

        {
            "bloodType": "O-",
            "requiredUnits": 10,
            "availableUnits": 78,
            "shortfallUnits": 0,
            "recommendation": "SUFFICIENT_STOCK",
            "suggestedAction": "RESERVE_STOCK_AND_PROCEED",
            "urgency": "Critical",
            "current_agent": "emergency_recommendation_agent"
        }
    """
    outbound = _validate_input(payload)

    client = client or InternalClient()

    result = client.post(
        RECOMMENDATION_PATH,
        outbound,
    )

    return _normalize_response(result)


def recommend(
    payload: dict[str, Any],
    client: InternalClient | None = None,
) -> dict[str, Any]:
    """Explicit alias for callers that prefer a verb-style function name."""
    return run(
        payload,
        client=client,
    )


def _validate_input(payload: Any) -> dict[str, Any]:
    if not isinstance(payload, dict):
        raise EmergencyRecommendationAgentError(
            "payload must be a dict"
        )

    missing = [
        key
        for key in _REQUIRED_FIELDS
        if payload.get(key) in (None, "")
    ]

    if missing:
        raise EmergencyRecommendationAgentError(
            "missing required field(s): "
            + ", ".join(missing)
        )

    blood_type = payload["bloodType"]

    if blood_type not in BLOOD_TYPE_TO_NUMBER:
        raise EmergencyRecommendationAgentError(
            "'bloodType' must be one of: "
            + ", ".join(BLOOD_TYPE_TO_NUMBER)
        )

    required_units = payload["requiredUnits"]

    if (
        not isinstance(required_units, int)
        or isinstance(required_units, bool)
        or required_units <= 0
    ):
        raise EmergencyRecommendationAgentError(
            "'requiredUnits' must be a positive integer"
        )

    urgency = payload["urgency"]

    if urgency not in URGENCY_TO_NUMBER:
        raise EmergencyRecommendationAgentError(
            "'urgency' must be one of: "
            + ", ".join(URGENCY_TO_NUMBER)
        )

    return {
        "bloodType": BLOOD_TYPE_TO_NUMBER[blood_type],
        "requiredUnits": required_units,
        "urgency": URGENCY_TO_NUMBER[urgency],
    }


def _normalize_response(result: Any) -> dict[str, Any]:
    if not isinstance(result, dict):
        raise InternalClientError(
            "inventory-recommendation response must be a JSON object"
        )

    blood_type = _read_blood_type(result)
    urgency = _read_urgency(result)

    required_units = _read_non_negative_int(
        result,
        "requiredUnits",
    )

    available_units = _read_non_negative_int(
        result,
        "availableUnits",
    )

    shortfall_units = _read_non_negative_int(
        result,
        "shortfallUnits",
        legacy_field="shortfall",
    )

    recommendation = result.get("recommendation")
    if not isinstance(recommendation, str):
        raise InternalClientError(
            "inventory-recommendation response field "
            "'recommendation' must be a string"
        )

    suggested_action = result.get("suggestedAction")

    if not isinstance(suggested_action, str):
        raise InternalClientError(
            "inventory-recommendation response field "
            "'suggestedAction' must be a string"
        )

    return {
        "bloodType": blood_type,
        "requiredUnits": required_units,
        "availableUnits": available_units,
        "shortfallUnits": shortfall_units,
        "recommendation": recommendation,
        "suggestedAction": suggested_action,
        "urgency": urgency,
        "current_agent": AGENT_NAME,
    }


def _read_blood_type(result: dict[str, Any]) -> str:
    value = result.get("bloodType")

    if isinstance(value, str):
        if value not in BLOOD_TYPE_TO_NUMBER:
            raise InternalClientError(
                "inventory-recommendation response field "
                "'bloodType' contains an invalid blood type"
            )

        return value

    if isinstance(value, int) and not isinstance(value, bool):
        blood_type = NUMBER_TO_BLOOD_TYPE.get(value)

        if blood_type is None:
            raise InternalClientError(
                "inventory-recommendation response field "
                "'bloodType' contains an invalid enum value"
            )

        return blood_type

    raise InternalClientError(
        "inventory-recommendation response field "
        "'bloodType' must be a blood-type string or enum number"
    )


def _read_urgency(result: dict[str, Any]) -> str:
    value = result.get("urgency")

    if value is None:
        value = result.get("priority")

    if isinstance(value, str):
        if value not in URGENCY_TO_NUMBER:
            raise InternalClientError(
                "inventory-recommendation response field "
                "'urgency' contains an invalid urgency"
            )

        return value

    if isinstance(value, int) and not isinstance(value, bool):
        urgency = NUMBER_TO_URGENCY.get(value)

        if urgency is None:
            raise InternalClientError(
                "inventory-recommendation response field "
                "'urgency' contains an invalid enum value"
            )

        return urgency

    raise InternalClientError(
        "inventory-recommendation response field "
        "'urgency' must be an urgency string or enum number"
    )


def _read_non_negative_int(
    result: dict[str, Any],
    field: str,
    legacy_field: str | None = None,
) -> int:
    value = result.get(field)

    if value is None and legacy_field is not None:
        value = result.get(legacy_field)

    if (
        not isinstance(value, int)
        or isinstance(value, bool)
        or value < 0
    ):
        if legacy_field is None:
            field_name = field
        else:
            field_name = f"{field} / {legacy_field}"

        raise InternalClientError(
            "inventory-recommendation response field "
            f"'{field_name}' must be a non-negative integer"
        )

    return value