from unittest.mock import patch

from agents.eligibility_validation_agent import run
from shared.internal_client import InternalCallError


@patch("agents.eligibility_validation_agent.call_internal")
def test_returns_eligible_and_excluded(mock_call):
    mock_call.return_value = {
        "eligible": [{"donorId": "d1", "reliabilityScore": 0.9}],
        "excluded": [{"donorId": "d2", "reason": "blood type mismatch"}],
    }
    result = run({"workflow_id": "w1", "candidate_donor_ids": ["d1", "d2"], "required_blood_type": "O-"})
    assert result["eligible_donor_ids"] == ["d1"]
    assert result["excluded_donors"][0]["reason"] == "blood type mismatch"
    assert result["error"] is None


@patch("agents.eligibility_validation_agent.call_internal")
def test_handles_no_candidates_without_calling_endpoint(mock_call):
    result = run({"workflow_id": "w1", "candidate_donor_ids": [], "required_blood_type": "O-"})
    assert result["eligible_donor_ids"] == []
    mock_call.assert_not_called()


@patch("agents.eligibility_validation_agent.call_internal")
def test_safe_failure_on_internal_error(mock_call):
    mock_call.side_effect = InternalCallError("timeout after retries")
    result = run({"workflow_id": "w1", "candidate_donor_ids": ["d1"], "required_blood_type": "O-"})
    assert result["error"] is not None
    assert result["eligible_donor_ids"] == []