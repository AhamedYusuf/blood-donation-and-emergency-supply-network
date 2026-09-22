"""Tests for Student 3's Stock-Check Agent.

The backend is replaced by a small fake client, so these tests require no
running ASP.NET Core service or database.
"""

import pytest

from agents.stock_check_agent import (
    CHECK_STOCK_PATH,
    StockCheckAgentError,
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


def valid_payload(**overrides):
    payload = {
        "workflowId": "wf-1",
        "requestingOrgId": "org-1",
        "bloodType": "O-",
        "unitsNeeded": 2,
    }
    payload.update(overrides)
    return payload


def valid_response(**overrides):
    response = {
        "sufficient": False,
        "ownStockUnits": 1,
        "shortfallUnits": 1,
        "candidateTransferOrgs": [
            {
                "organizationId": "org-2",
                "distanceKm": 4.2,
                "unitsAvailable": 3,
            }
        ],
    }
    response.update(overrides)
    return response


def test_run_forwards_exact_spec_contract():
    client = FakeClient(response=valid_response())

    result = run(valid_payload(), client=client)

    assert client.calls == [
        (
            CHECK_STOCK_PATH,
            {
                "workflowId": "wf-1",
                "requestingOrgId": "org-1",
                "bloodType": "O-",
                "unitsNeeded": 2,
            },
        )
    ]
    assert result["sufficient"] is False
    assert result["ownStockUnits"] == 1
    assert result["shortfallUnits"] == 1
    assert result["candidateTransferOrgs"][0]["distanceKm"] == 4.2
    assert result["current_agent"] == "stock_check_agent"


@pytest.mark.parametrize(
    "missing",
    ["workflowId", "requestingOrgId", "bloodType", "unitsNeeded"],
)
def test_run_rejects_missing_required_fields(missing):
    payload = valid_payload()
    del payload[missing]

    with pytest.raises(StockCheckAgentError):
        run(payload, client=FakeClient())


@pytest.mark.parametrize(
    "blood_type",
    ["", "A", "a+", "XYZ", None, 123],
)
def test_run_rejects_invalid_blood_type(blood_type):
    with pytest.raises(StockCheckAgentError):
        run(valid_payload(bloodType=blood_type), client=FakeClient())


@pytest.mark.parametrize(
    "units",
    [0, -1, 2.5, "2", True, None],
)
def test_run_rejects_invalid_units(units):
    with pytest.raises(StockCheckAgentError):
        run(valid_payload(unitsNeeded=units), client=FakeClient())


def test_run_propagates_backend_client_error():
    client = FakeClient(error=InternalClientError("backend unavailable"))

    with pytest.raises(InternalClientError):
        run(valid_payload(), client=client)


@pytest.mark.parametrize(
    "bad_response",
    [
        {},
        {"sufficient": False},
        {
            "sufficient": False,
            "ownStockUnits": 1,
            "shortfallUnits": 1,
            "candidateTransferOrgs": "not-a-list",
        },
        {
            "sufficient": "false",
            "ownStockUnits": 1,
            "shortfallUnits": 1,
            "candidateTransferOrgs": [],
        },
    ],
)
def test_run_rejects_invalid_backend_response(bad_response):
    with pytest.raises(InternalClientError):
        run(valid_payload(), client=FakeClient(response=bad_response))
