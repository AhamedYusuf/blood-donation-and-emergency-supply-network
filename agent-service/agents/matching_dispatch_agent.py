"""Matching & Dispatch Agent — Student 4.

Two modes, per Tech Doc §4.4, called at two different points in the
Coordinator's pipeline:

* :func:`search`   – pre-approval. Asks the backend for a ranked list of
  candidate donors. Calls ``POST /api/internal/agent/search-donors``.
* :func:`dispatch` – post-approval only. Sends FCM alerts and creates
  appointment invites. Calls ``POST /api/internal/agent/dispatch``.

Each function is a thin LangGraph node: it validates its input, calls the
internal .NET endpoint through :class:`InternalClient`, and returns the
structured response unchanged. Every real decision — the ranking maths,
the "workflow must be approved" guard, the FCM calls — lives server-side
in C#; this module deliberately holds no business logic.
"""

from __future__ import annotations

from typing import Any

from shared.internal_client import InternalClient, InternalClientError

SEARCH_PATH = "/api/internal/agent/search-donors"
DISPATCH_PATH = "/api/internal/agent/dispatch"

_SEARCH_REQUIRED = ("workflowId", "bloodType", "unitsNeeded", "location", "urgencyLevel")
_DISPATCH_REQUIRED = ("workflowId", "eligible")


def search(
    payload: dict[str, Any], client: InternalClient | None = None
) -> dict[str, Any]:
    """Run the pre-approval donor search.

    ``payload`` must contain ``workflowId``, ``bloodType``, ``unitsNeeded``
    (positive int), ``location`` (``{"lat": float, "lng": float}``) and
    ``urgencyLevel``. ``radiusKm`` is optional and passed through.
    """
    _require_fields(payload, _SEARCH_REQUIRED)
    _require_positive_int(payload, "unitsNeeded")

    location = payload["location"]
    if not isinstance(location, dict) or "lat" not in location or "lng" not in location:
        raise ValueError("'location' must be an object with 'lat' and 'lng'")

    client = client or InternalClient()
    result = client.post(SEARCH_PATH, payload)

    if not isinstance(result, dict) or "candidates" not in result:
        raise InternalClientError(
            "search-donors response did not contain a 'candidates' list"
        )
    return result


def dispatch(
    payload: dict[str, Any], client: InternalClient | None = None
) -> dict[str, Any]:
    """Run the post-approval dispatch.

    ``payload`` must contain ``workflowId`` and ``eligible`` (the list
    returned by the Eligibility Validation Agent). The backend rejects the
    call with 403/409 if the workflow is not in the ``approved`` state —
    that surfaces here as :class:`InternalClientError`.
    """
    _require_fields(payload, _DISPATCH_REQUIRED)

    if not isinstance(payload["eligible"], list):
        raise ValueError("'eligible' must be a list of donor entries")

    client = client or InternalClient()
    return client.post(DISPATCH_PATH, payload)


def _require_fields(payload: Any, keys: tuple[str, ...]) -> None:
    if not isinstance(payload, dict):
        raise ValueError("payload must be a dict")
    missing = [k for k in keys if payload.get(k) in (None, "")]
    if missing:
        raise ValueError(f"missing required field(s): {', '.join(missing)}")


def _require_positive_int(payload: dict[str, Any], key: str) -> None:
    value = payload.get(key)
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        raise ValueError(f"'{key}' must be a positive integer")
