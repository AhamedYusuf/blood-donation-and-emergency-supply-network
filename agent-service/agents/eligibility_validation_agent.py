from __future__ import annotations

from typing import Any

from shared.internal_client import InternalCallError, call_internal

AGENT_NAME = "eligibility_validation_agent"
VALIDATE_PATH = "/api/internal/agent/validate-eligibility"


def run(state: dict[str, Any]) -> dict[str, Any]:
	"""Validate matching candidates through the deterministic backend rule engine."""
	candidate_ids = state.get("candidate_donor_ids", [])
	if not candidate_ids:
		return _result([], [], None)

	payload = {
		"workflowId": state.get("workflow_id"),
		"candidateDonorIds": candidate_ids,
		"requiredBloodType": state.get("required_blood_type"),
	}

	try:
		response = call_internal(VALIDATE_PATH, payload)
	except InternalCallError as exc:
		return _result([], [], str(exc))

	eligible = response.get("eligible", [])
	excluded = response.get("excluded", [])
	return _result([donor["donorId"] for donor in eligible], excluded, None)


def _result(eligible_ids: list[str], excluded: list[dict[str, Any]], error: str | None) -> dict[str, Any]:
	return {
		"eligible_donor_ids": eligible_ids,
		"excluded_donors": excluded,
		"current_agent": AGENT_NAME,
		"error": error,
	}
