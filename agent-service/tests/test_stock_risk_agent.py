import pytest

from agents.stock_risk_agent import (
    STOCK_RISK_PATH,
    StockRiskAgentError,
    run,
)
from shared.internal_client import InternalClientError


class FakeClient:
    def __init__(self, response=None, error=None):
        self.response = response if response is not None else {}
        self.error = error
        self.calls = []

    def get(self, path):
        self.calls.append(path)
        if self.error is not None:
            raise self.error
        return self.response


def valid_response():
    return {
        "organizationId": "b9a8c39b-b1bc-415b-82f4-ab7021a258c0",
        "riskLevel": "HIGH",
        "totalUnits": 20,
        "lowStockTypes": 2,
        "atRiskInventory": [],
        "recommendation": "Prioritize replenishment.",
    }


def test_run_calls_stock_risk_endpoint():
    client = FakeClient(response=valid_response())
    result = run(
        {"organizationId": "b9a8c39b-b1bc-415b-82f4-ab7021a258c0"},
        client=client,
    )

    assert result["current_agent"] == "stock_risk_agent"
    assert client.calls == [
        STOCK_RISK_PATH.format(
            organization_id="b9a8c39b-b1bc-415b-82f4-ab7021a258c0"
        )
    ]


def test_rejects_missing_organization_id():
    with pytest.raises(StockRiskAgentError):
        run({}, client=FakeClient(response=valid_response()))


@pytest.mark.parametrize(
    "organization_id",
    ["not-a-uuid", 12, None, ""],
)
def test_rejects_invalid_organization_id(organization_id):
    with pytest.raises(StockRiskAgentError):
        run({"organizationId": organization_id}, client=FakeClient())


def test_rejects_incomplete_response():
    with pytest.raises(InternalClientError):
        run(
            {"organizationId": "b9a8c39b-b1bc-415b-82f4-ab7021a258c0"},
            client=FakeClient(response={"riskLevel": "HIGH"}),
        )


def test_surfaces_backend_error():
    client = FakeClient(error=InternalClientError("boom"))
    with pytest.raises(InternalClientError):
        run(
            {"organizationId": "b9a8c39b-b1bc-415b-82f4-ab7021a258c0"},
            client=client,
        )
