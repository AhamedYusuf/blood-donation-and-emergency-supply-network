"""Helper for calling the ASP.NET Core backend's ``/api/internal/*`` endpoints.

Those endpoints are protected by ``InternalSecretMiddleware`` on the .NET
side: every request must carry an ``X-Internal-Secret`` header whose value
matches the backend's ``InternalAgentSecret`` configuration. A normal user
JWT is not accepted. This client attaches that header and normalises
failures into :class:`InternalClientError`.

Configuration (environment variables):

* ``BACKEND_BASE_URL``     – base URL of the .NET API (default
  ``http://localhost:5067``)
* ``INTERNAL_AGENT_SECRET`` – the shared secret; required, no default
"""

from __future__ import annotations

import os

import httpx

DEFAULT_BASE_URL = "http://localhost:5067"
DEFAULT_TIMEOUT_SECONDS = 30.0


class InternalClientError(RuntimeError):
    """Raised for any failed internal call: transport error, non-2xx
    response, or an unparseable body."""


class InternalClient:
    def __init__(
        self,
        base_url: str | None = None,
        secret: str | None = None,
        timeout: float = DEFAULT_TIMEOUT_SECONDS,
    ) -> None:
        self._base_url = (
            base_url or os.environ.get("BACKEND_BASE_URL", DEFAULT_BASE_URL)
        ).rstrip("/")
        self._secret = secret if secret is not None else os.environ.get(
            "INTERNAL_AGENT_SECRET", ""
        )
        self._timeout = timeout

    def post(self, path: str, payload: dict) -> dict:
        """POST ``payload`` as JSON to ``path`` and return the parsed JSON body.

        Raises :class:`InternalClientError` if the secret is not configured,
        the request cannot be made, the response status is >= 400, or the
        body is not JSON.
        """
        if not self._secret:
            raise InternalClientError(
                "INTERNAL_AGENT_SECRET is not set — cannot call internal endpoints"
            )

        url = f"{self._base_url}/{path.lstrip('/')}"
        headers = {"X-Internal-Secret": self._secret}

        try:
            response = httpx.post(
                url, json=payload, headers=headers, timeout=self._timeout
            )
        except httpx.RequestError as exc:
            raise InternalClientError(f"request to {url} failed: {exc}") from exc

        if response.status_code >= 400:
            raise InternalClientError(
                f"{path} returned HTTP {response.status_code}: {response.text}"
            )

        try:
            return response.json()
        except ValueError as exc:
            raise InternalClientError(
                f"{path} returned a non-JSON body"
            ) from exc
