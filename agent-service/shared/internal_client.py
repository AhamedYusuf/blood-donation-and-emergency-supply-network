"""Helper for calling the ASP.NET Core backend's ``/api/internal/*`` endpoints.

Those endpoints are protected by ``InternalSecretMiddleware`` on the .NET
side. Every request must carry an ``X-Internal-Secret`` header whose value
matches the backend's ``InternalAgentSecret`` configuration.

Configuration:

* ``BACKEND_BASE_URL`` – base URL of the .NET API
  (default: ``http://localhost:5067``)
* ``INTERNAL_AGENT_SECRET`` – shared internal secret
"""

from __future__ import annotations

import os
import time
from typing import Any

import httpx


DEFAULT_BASE_URL = "http://localhost:5067"
DEFAULT_TIMEOUT_SECONDS = 30.0
MAX_RETRIES = 2


class InternalClientError(RuntimeError):
    """Raised for failed internal agent API calls."""


# Existing agents in the project use this name.
# Keep it as an alias for backward compatibility.
InternalCallError = InternalClientError


class InternalClient:
    """HTTP client for the backend's internal agent endpoints."""

    def __init__(
        self,
        base_url: str | None = None,
        secret: str | None = None,
        timeout: float = DEFAULT_TIMEOUT_SECONDS,
    ) -> None:
        self._base_url = (
            base_url
            or os.environ.get(
                "BACKEND_BASE_URL",
                DEFAULT_BASE_URL,
            )
        ).rstrip("/")

        self._secret = (
            secret
            if secret is not None
            else os.environ.get(
                "INTERNAL_AGENT_SECRET",
                "",
            )
        )

        self._timeout = timeout

    def _build_url(self, path: str) -> str:
        """Build a full backend URL from a relative path."""
        return f"{self._base_url}/{path.lstrip('/')}"

    def _build_headers(self) -> dict[str, str]:
        """Build the internal-secret request headers."""
        if not self._secret:
            raise InternalClientError(
                "INTERNAL_AGENT_SECRET is not set "
                "— cannot call internal endpoints"
            )

        return {
            "X-Internal-Secret": self._secret,
        }

    @staticmethod
    def _parse_response(
        response: httpx.Response,
        path: str,
    ) -> dict[str, Any]:
        """Validate an HTTP response and return its JSON body."""

        if response.status_code >= 400:
            raise InternalClientError(
                f"{path} returned HTTP "
                f"{response.status_code}: {response.text}"
            )

        try:
            result = response.json()
        except ValueError as exc:
            raise InternalClientError(
                f"{path} returned a non-JSON body"
            ) from exc

        if not isinstance(result, dict):
            raise InternalClientError(
                f"{path} returned JSON in an unexpected format"
            )

        return result

    def post(
        self,
        path: str,
        payload: dict[str, Any],
    ) -> dict[str, Any]:
        """POST a JSON payload to an internal endpoint.

        Returns the parsed JSON object.

        Raises:
            InternalClientError:
                When the secret is missing, the request fails,
                the backend returns an error, or the body is invalid.
        """

        url = self._build_url(path)
        headers = self._build_headers()

        last_error: Exception | None = None

        for attempt in range(MAX_RETRIES + 1):
            try:
                # IMPORTANT:
                # Keep httpx.post() here because the existing project
                # tests monkeypatch httpx.post directly.
                response = httpx.post(
                    url,
                    json=payload,
                    headers=headers,
                    timeout=self._timeout,
                )

                return self._parse_response(
                    response,
                    path,
                )

            except httpx.RequestError as exc:
                last_error = exc

                if attempt == MAX_RETRIES:
                    raise InternalClientError(
                        f"request to {url} failed after "
                        f"{MAX_RETRIES + 1} attempts: {exc}"
                    ) from exc

                time.sleep(0.5 * (attempt + 1))

        raise InternalClientError(
            f"request to {url} failed: {last_error}"
        )

    def get(
        self,
        path: str,
    ) -> dict[str, Any]:
        """GET an internal endpoint.

        Returns the parsed JSON object.

        This is used by agents such as Stock Risk Agent
        when calling a backend GET endpoint.
        """

        url = self._build_url(path)
        headers = self._build_headers()

        last_error: Exception | None = None

        for attempt in range(MAX_RETRIES + 1):
            try:
                response = httpx.get(
                    url,
                    headers=headers,
                    timeout=self._timeout,
                )

                return self._parse_response(
                    response,
                    path,
                )

            except httpx.RequestError as exc:
                last_error = exc

                if attempt == MAX_RETRIES:
                    raise InternalClientError(
                        f"request to {url} failed after "
                        f"{MAX_RETRIES + 1} attempts: {exc}"
                    ) from exc

                time.sleep(0.5 * (attempt + 1))

        raise InternalClientError(
            f"request to {url} failed: {last_error}"
        )


def call_internal(
    path: str,
    payload: dict[str, Any],
    timeout: float = DEFAULT_TIMEOUT_SECONDS,
) -> dict[str, Any]:
    """Call an internal POST endpoint using the shared configured client.

    This function is kept for compatibility with the existing agents.
    """

    return InternalClient(timeout=timeout).post(
        path,
        payload,
    )