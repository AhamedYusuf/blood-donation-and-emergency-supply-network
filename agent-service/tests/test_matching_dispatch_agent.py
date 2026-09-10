"""Tests for the Matching & Dispatch agent node (Student 4).

The internal HTTP client is replaced with an in-memory fake, so these run
without a backend. Tech Doc §4.7 asks for tests of the dispatch-mode guard
and input validation; the guard itself is enforced server-side, so here we
assert the node surfaces the backend's rejection rather than swallowing it.
"""

import pytest

from agents.matching_dispatch_agent import (
    DISPATCH_PATH,
    SEARCH_PATH,
    dispatch,
    search,
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


def valid_search_payload(**overrides):
    payload = {
        "workflowId": "wf-1",
        "bloodType": "O-",
        "unitsNeeded": 2,
        "location": {"lat": 6.9271, "lng": 79.8612},
        "urgencyLevel": "critical",
        "radiusKm": 15,
    }
    payload.update(overrides)
    return payload


# --------------------------------------------------------------------- search

def test_search_forwards_payload_to_the_search_endpoint():
    client = FakeClient(response={"candidates": [{"donorId": "d1", "rank": 1}]})

    result = search(valid_search_payload(), client=client)

    assert result == {"candidates": [{"donorId": "d1", "rank": 1}]}
    assert client.calls == [(SEARCH_PATH, valid_search_payload())]


@pytest.mark.parametrize("missing", ["workflowId", "bloodType", "unitsNeeded", "location", "urgencyLevel"])
def test_search_rejects_missing_required_field(missing):
    payload = valid_search_payload()
    del payload[missing]

    with pytest.raises(ValueError):
        search(payload, client=FakeClient())


@pytest.mark.parametrize("bad_units", [0, -1, 2.5, "2", True])
def test_search_rejects_non_positive_int_units(bad_units):
    with pytest.raises(ValueError):
        search(valid_search_payload(unitsNeeded=bad_units), client=FakeClient())


@pytest.mark.parametrize("bad_location", [None, "here", {"lat": 1.0}, {"lng": 2.0}, 42])
def test_search_rejects_malformed_location(bad_location):
    with pytest.raises(ValueError):
        search(valid_search_payload(location=bad_location), client=FakeClient())


def test_search_raises_when_response_has_no_candidates():
    with pytest.raises(InternalClientError):
        search(valid_search_payload(), client=FakeClient(response={"unexpected": True}))


def test_search_propagates_client_errors():
    client = FakeClient(error=InternalClientError("boom"))

    with pytest.raises(InternalClientError):
        search(valid_search_payload(), client=client)


# ------------------------------------------------------------------- dispatch

def test_dispatch_forwards_payload_to_the_dispatch_endpoint():
    payload = {"workflowId": "wf-1", "eligible": [{"donorId": "d1"}]}
    client = FakeClient(response={"notifiedDonorIds": ["d1"], "appointmentsCreated": ["a1"]})

    result = dispatch(payload, client=client)

    assert result["notifiedDonorIds"] == ["d1"]
    assert client.calls == [(DISPATCH_PATH, payload)]


@pytest.mark.parametrize("missing", ["workflowId", "eligible"])
def test_dispatch_rejects_missing_required_field(missing):
    payload = {"workflowId": "wf-1", "eligible": []}
    del payload[missing]

    with pytest.raises(ValueError):
        dispatch(payload, client=FakeClient())


def test_dispatch_rejects_non_list_eligible():
    with pytest.raises(ValueError):
        dispatch({"workflowId": "wf-1", "eligible": {"donorId": "d1"}}, client=FakeClient())


def test_dispatch_surfaces_backend_rejection_for_unapproved_workflow():
    # The backend returns 403/409 when the workflow is not approved;
    # InternalClient turns that into InternalClientError. The node must not
    # swallow it.
    client = FakeClient(error=InternalClientError("dispatch returned HTTP 409: not approved"))

    with pytest.raises(InternalClientError):
        dispatch({"workflowId": "wf-1", "eligible": [{"donorId": "d1"}]}, client=client)
