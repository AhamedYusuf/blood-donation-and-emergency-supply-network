"""Tests for the shared InternalClient — no real network calls.

``httpx.post`` is monkeypatched so we can assert on the request the client
builds (URL, the X-Internal-Secret header) and on how it maps responses
and transport failures onto InternalClientError.
"""

import httpx
import pytest

from shared import internal_client
from shared.internal_client import InternalClient, InternalClientError


class FakeResponse:
    def __init__(self, status_code=200, json_body=None, text=""):
        self.status_code = status_code
        self._json_body = json_body
        self.text = text

    def json(self):
        if self._json_body is None:
            raise ValueError("no json")
        return self._json_body


def test_post_attaches_secret_header_and_returns_parsed_json(monkeypatch):
    captured = {}

    def fake_post(url, json, headers, timeout):
        captured["url"] = url
        captured["json"] = json
        captured["headers"] = headers
        return FakeResponse(200, {"ok": True})

    monkeypatch.setattr(internal_client.httpx, "post", fake_post)

    client = InternalClient(base_url="http://backend:5067/", secret="s3cret")
    result = client.post("/api/internal/agent/search-donors", {"a": 1})

    assert result == {"ok": True}
    assert captured["url"] == "http://backend:5067/api/internal/agent/search-donors"
    assert captured["json"] == {"a": 1}
    assert captured["headers"]["X-Internal-Secret"] == "s3cret"


def test_post_without_secret_raises_before_any_request(monkeypatch):
    def fail(*_args, **_kwargs):  # pragma: no cover - must not be called
        raise AssertionError("httpx.post should not be called without a secret")

    monkeypatch.setattr(internal_client.httpx, "post", fail)

    with pytest.raises(InternalClientError):
        InternalClient(secret="").post("/api/internal/agent/dispatch", {})


def test_post_maps_non_2xx_to_internal_client_error(monkeypatch):
    monkeypatch.setattr(
        internal_client.httpx,
        "post",
        lambda *_, **__: FakeResponse(409, text="workflow not approved"),
    )

    with pytest.raises(InternalClientError) as exc:
        InternalClient(secret="s").post("/api/internal/agent/dispatch", {})

    assert "409" in str(exc.value)


def test_post_maps_transport_error_to_internal_client_error(monkeypatch):
    def raise_request_error(*_, **__):
        raise httpx.ConnectError("connection refused")

    monkeypatch.setattr(internal_client.httpx, "post", raise_request_error)

    with pytest.raises(InternalClientError):
        InternalClient(secret="s").post("/api/internal/agent/search-donors", {})


def test_post_maps_non_json_body_to_internal_client_error(monkeypatch):
    monkeypatch.setattr(
        internal_client.httpx, "post", lambda *_, **__: FakeResponse(200, json_body=None)
    )

    with pytest.raises(InternalClientError):
        InternalClient(secret="s").post("/api/internal/agent/search-donors", {})


def test_base_url_falls_back_to_env(monkeypatch):
    monkeypatch.setenv("BACKEND_BASE_URL", "http://from-env:9000")
    monkeypatch.setenv("INTERNAL_AGENT_SECRET", "envsecret")
    monkeypatch.setattr(
        internal_client.httpx,
        "post",
        lambda url, json, headers, timeout: FakeResponse(200, {"url": url, "secret": headers["X-Internal-Secret"]}),
    )

    result = InternalClient().post("/health", {})

    assert result == {"url": "http://from-env:9000/health", "secret": "envsecret"}
