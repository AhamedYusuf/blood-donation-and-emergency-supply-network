import pytest

from agents.emergency_recommendation_agent import (
    BLOOD_TYPE_TO_NUMBER,
    RECOMMENDATION_PATH,
    EmergencyRecommendationAgentError,
    run,
)
from shared.internal_client import InternalClientError


class FakeClient:
    def __init__(self, response=None, error=None):
        self.response = response if response is not None else {}
        self.error = error
        self.calls = []

    def post(self, path, payload):
        self.calls.append((path, payload))
        if self.error is not None:
            raise self.error
        return self.response


def valid_response():
    return {
        "bloodType": 7,
        "requiredUnits": 10,
        "availableUnits": 3,
        "shortfall": 7,
        "recommendation": "PARTIAL_STOCK",
        "suggestedAction": "REQUEST_TRANSFER",
        "priority": 1,
    }


def test_run_maps_contract_to_backend_enum_numbers():
    client = FakeClient(response=valid_response())

    result = run(
        {
            "bloodType": "O-",
            "requiredUnits": 10,
            "urgency": "Urgent",
        },
        client=client,
    )

    assert result["current_agent"] == "emergency_recommendation_agent"
    assert client.calls == [
        (
            RECOMMENDATION_PATH,
            {
                "bloodType": BLOOD_TYPE_TO_NUMBER["O-"],
                "requiredUnits": 10,
                "urgency": 1,
            },
        )
    ]


@pytest.mark.parametrize("missing", ["bloodType", "requiredUnits", "urgency"])
def test_rejects_missing_required_field(missing):
    payload = {"bloodType": "O-", "requiredUnits": 10, "urgency": "Urgent"}
    del payload[missing]

    with pytest.raises(EmergencyRecommendationAgentError):
        run(payload, client=FakeClient(response=valid_response()))


@pytest.mark.parametrize("bad_units", [0, -1, 2.5, "2", True])
def test_rejects_bad_units(bad_units):
    with pytest.raises(EmergencyRecommendationAgentError):
        run(
            {"bloodType": "O-", "requiredUnits": bad_units, "urgency": "Urgent"},
            client=FakeClient(response=valid_response()),
        )


def test_rejects_unknown_blood_type():
    with pytest.raises(EmergencyRecommendationAgentError):
        run(
            {"bloodType": "X", "requiredUnits": 2, "urgency": "Urgent"},
            client=FakeClient(response=valid_response()),
        )


def test_rejects_unknown_urgency():
    with pytest.raises(EmergencyRecommendationAgentError):
        run(
            {"bloodType": "O-", "requiredUnits": 2, "urgency": "ASAP"},
            client=FakeClient(response=valid_response()),
        )


def test_rejects_incomplete_response():
    with pytest.raises(InternalClientError):
        run(
            {"bloodType": "O-", "requiredUnits": 2, "urgency": "Urgent"},
            client=FakeClient(response={"shortfall": 2}),
        )


def test_surfaces_backend_error():
    client = FakeClient(error=InternalClientError("boom"))
    with pytest.raises(InternalClientError):
        run(
            {"bloodType": "O-", "requiredUnits": 2, "urgency": "Urgent"},
            client=client,
        )
