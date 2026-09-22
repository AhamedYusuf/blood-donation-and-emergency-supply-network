from typing import Any

from shared.internal_client import InternalClient


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


def run(request: dict[str, Any]) -> dict[str, Any]:

    print("\n" + "=" * 70)
    print("[STOCK CHECK AGENT] REQUEST RECEIVED")
    print(request)
    print("=" * 70)

    # --------------------------------------------------
    # Get values
    # --------------------------------------------------

    blood_type = request.get("bloodType")

    required_units = request.get("requiredUnits")

    organization_id = (
        request.get("organizationId")
        or request.get("requestingOrgId")
    )

    workflow_id = (
        request.get("workflowId")
        or ""
    )

    # --------------------------------------------------
    # Validate blood type
    # --------------------------------------------------

    if not blood_type:
        raise ValueError(
            "bloodType is required"
        )

    if blood_type not in BLOOD_TYPE_TO_NUMBER:
        raise ValueError(
            f"Unsupported blood type: {blood_type}. "
            f"Expected one of: "
            f"{', '.join(BLOOD_TYPE_TO_NUMBER.keys())}"
        )

    # --------------------------------------------------
    # Validate required units
    # --------------------------------------------------

    if required_units is None:
        raise ValueError(
            "requiredUnits is required"
        )

    try:
        required_units = int(required_units)
    except (TypeError, ValueError):
        raise ValueError(
            "requiredUnits must be a valid integer"
        )

    if required_units <= 0:
        raise ValueError(
            "requiredUnits must be greater than 0"
        )

    # --------------------------------------------------
    # Validate organization
    # --------------------------------------------------

    if not organization_id:
        raise ValueError(
            "organizationId is required"
        )

    # --------------------------------------------------
    # Convert blood type
    # --------------------------------------------------

    blood_type_number = (
        BLOOD_TYPE_TO_NUMBER[blood_type]
    )

    # --------------------------------------------------
    # Backend payload
    # --------------------------------------------------

    payload = {
        "organizationId": organization_id,
        "bloodType": blood_type_number,
        "requiredUnits": required_units,
    }

    print("\n[STOCK CHECK AGENT] BACKEND PAYLOAD")
    print(payload)

    # --------------------------------------------------
    # Call backend
    # --------------------------------------------------

    client = InternalClient()

    try:

        result = client.post(
            "/api/internal/agent/check-stock",
            payload,
        )

    except Exception as exc:

        print("\n" + "=" * 70)
        print("[STOCK CHECK AGENT] BACKEND ERROR")
        print(str(exc))
        print("=" * 70)

        raise

    # --------------------------------------------------
    # Result
    # --------------------------------------------------

    print("\n[STOCK CHECK AGENT] BACKEND RESULT")
    print(result)

    return result