import { useMemo, useState } from "react";
import { useParams } from "react-router-dom";

import {
  useApproveWorkflowMutation,
  useGetWorkflowQuery,
  useGetWorkflowStepsQuery,
  useGetWorkflowSummaryQuery,
  useRejectWorkflowMutation,
  useReviseWorkflowMutation,
} from "./workflowMonitorApi";

import "./workflowMonitor.css";


function formatDate(value?: string | null) {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString();
}


function formatStepName(value: string) {
  return value
    .replaceAll("_", " ")
    .replace(/\b\w/g, (letter) =>
      letter.toUpperCase()
    );
}


export default function WorkflowMonitorPage() {
  const { id } = useParams();

  const [comments, setComments] = useState("");
  const [message, setMessage] = useState("");
  const [errorMessage, setErrorMessage] =
    useState("");

  const {
    data: workflow,
    isLoading: workflowLoading,
    error: workflowError,
    refetch: refetchWorkflow,
  } = useGetWorkflowQuery(id ?? "", {
    skip: !id,
    pollingInterval: 5000,
  });

  const {
    data: steps = [],
    isLoading: stepsLoading,
    refetch: refetchSteps,
  } = useGetWorkflowStepsQuery(id ?? "", {
    skip: !id,
    pollingInterval: 5000,
  });

  const {
    data: summary,
    refetch: refetchSummary,
  } = useGetWorkflowSummaryQuery(id ?? "", {
    skip: !id,
    pollingInterval: 5000,
  });

  const [
    approveWorkflow,
    {
      isLoading: approving,
    },
  ] = useApproveWorkflowMutation();

  const [
    rejectWorkflow,
    {
      isLoading: rejecting,
    },
  ] = useRejectWorkflowMutation();

  const [
    reviseWorkflow,
    {
      isLoading: revising,
    },
  ] = useReviseWorkflowMutation();


  const actionLoading =
    approving ||
    rejecting ||
    revising;


  const canMakeDecision = useMemo(() => {
    if (!workflow) {
      return false;
    }

    const status =
      workflow.status.toLowerCase();

    return (
      status === "planning" ||
      status === "awaiting_approval"
    );
  }, [workflow]);


  async function refreshAll() {
    await Promise.all([
      refetchWorkflow(),
      refetchSteps(),
      refetchSummary(),
    ]);
  }


  async function handleDecision(
    decision:
      | "approve"
      | "reject"
      | "revise"
  ) {
    if (!id) {
      return;
    }

    setMessage("");
    setErrorMessage("");

    try {
      const payload = {
        id,
        body: {
          comments:
            comments.trim() || null,
        },
      };

      if (decision === "approve") {
        await approveWorkflow(
          payload
        ).unwrap();

        setMessage(
          "Workflow approved successfully."
        );
      }

      if (decision === "reject") {
        await rejectWorkflow(
          payload
        ).unwrap();

        setMessage(
          "Workflow rejected successfully."
        );
      }

      if (decision === "revise") {
        await reviseWorkflow(
          payload
        ).unwrap();

        setMessage(
          "Workflow revision requested successfully."
        );
      }

      setComments("");

      await refreshAll();

    } catch (error: any) {
      const apiMessage =
        error?.data?.message;

      setErrorMessage(
        apiMessage ??
          "The workflow action failed."
      );
    }
  }


  if (!id) {
    return (
      <div className="workflow-monitor-page">
        <h1>Workflow Monitor</h1>

        <div className="workflow-alert error">
          Workflow ID is missing.
        </div>
      </div>
    );
  }


  if (workflowLoading) {
    return (
      <div className="workflow-monitor-page">
        <h1>Workflow Monitor</h1>

        <p>Loading workflow...</p>
      </div>
    );
  }


  if (workflowError || !workflow) {
    return (
      <div className="workflow-monitor-page">
        <h1>Workflow Monitor</h1>

        <div className="workflow-alert error">
          Workflow could not be loaded.
        </div>
      </div>
    );
  }


  return (
    <div className="workflow-monitor-page">
      <div className="workflow-header">
        <div>
          <h1>Agent Workflow Monitor</h1>

          <p>
            Monitor the coordinator workflow,
            agent steps and human approval.
          </p>
        </div>

        <button
          type="button"
          className="workflow-refresh-button"
          onClick={refreshAll}
        >
          Refresh
        </button>
      </div>


      {message && (
        <div className="workflow-alert success">
          {message}
        </div>
      )}

      {errorMessage && (
        <div className="workflow-alert error">
          {errorMessage}
        </div>
      )}


      <section className="workflow-card">
        <h2>Workflow Information</h2>

        <div className="workflow-info-grid">
          <div>
            <span className="workflow-label">
              Workflow ID
            </span>

            <strong>
              {workflow.id}
            </strong>
          </div>

          <div>
            <span className="workflow-label">
              Blood Request
            </span>

            <strong>
              {workflow.bloodRequestId}
            </strong>
          </div>

          <div>
            <span className="workflow-label">
              Objective
            </span>

            <strong>
              {workflow.objective || "-"}
            </strong>
          </div>

          <div>
            <span className="workflow-label">
              Current Agent
            </span>

            <strong>
              {workflow.currentAgent || "-"}
            </strong>
          </div>

          <div>
            <span className="workflow-label">
              Status
            </span>

            <strong
              className={`workflow-status ${workflow.status.toLowerCase()}`}
            >
              {workflow.status}
            </strong>
          </div>

          <div>
            <span className="workflow-label">
              Revision Count
            </span>

            <strong>
              {workflow.revisionCount}
            </strong>
          </div>

          <div>
            <span className="workflow-label">
              Started
            </span>

            <strong>
              {formatDate(
                workflow.startedAt
              )}
            </strong>
          </div>

          <div>
            <span className="workflow-label">
              Updated
            </span>

            <strong>
              {formatDate(
                workflow.updatedAt
              )}
            </strong>
          </div>
        </div>

        {workflow.failureReason && (
          <div className="workflow-failure">
            <strong>
              Failure reason:
            </strong>{" "}
            {workflow.failureReason}
          </div>
        )}
      </section>


      <section className="workflow-card">
        <h2>Workflow Summary</h2>

        {summary ? (
          <div className="workflow-summary-grid">
            <div>
              <span>Total Steps</span>

              <strong>
                {summary.totalSteps}
              </strong>
            </div>

            <div>
              <span>Completed</span>

              <strong>
                {summary.completedSteps}
              </strong>
            </div>

            <div>
              <span>Failed</span>

              <strong>
                {summary.failedSteps}
              </strong>
            </div>

            <div>
              <span>Revisions</span>

              <strong>
                {summary.revisionCount}
              </strong>
            </div>
          </div>
        ) : (
          <p>
            Summary is not available yet.
          </p>
        )}

        {summary?.latestDecision && (
          <div className="workflow-latest-decision">
            <strong>
              Latest human decision:
            </strong>

            <p>
              {
                summary.latestDecision
                  .decision
              }
            </p>

            {summary.latestDecision
              .comments && (
              <p>
                Comments:{" "}
                {
                  summary.latestDecision
                    .comments
                }
              </p>
            )}
          </div>
        )}
      </section>


      <section className="workflow-card">
        <h2>Agent Steps</h2>

        {stepsLoading ? (
          <p>
            Loading agent steps...
          </p>
        ) : steps.length === 0 ? (
          <p>
            No workflow steps have been
            recorded yet.
          </p>
        ) : (
          <div className="workflow-steps">
            {steps.map((step, index) => (
              <div
                className="workflow-step"
                key={step.id}
              >
                <div className="workflow-step-number">
                  {index + 1}
                </div>

                <div className="workflow-step-content">
                  <div className="workflow-step-header">
                    <div>
                      <h3>
                        {formatStepName(
                          step.stepName
                        )}
                      </h3>

                      <span>
                        {step.agentName}
                      </span>
                    </div>

                    <strong
                      className={`workflow-step-status ${step.status.toLowerCase()}`}
                    >
                      {step.status}
                    </strong>
                  </div>

                  {step.narrative && (
                    <p>
                      {step.narrative}
                    </p>
                  )}

                  <div className="workflow-step-times">
                    <span>
                      Retry Count: {step.retryCount}
                    </span>

                    <span>
                      Started:{" "}
                      {formatDate(
                        step.startedAt
                      )}
                    </span>

                    <span>
                      Completed:{" "}
                      {formatDate(
                        step.completedAt
                      )}
                    </span>
                  </div>

                  {step.errorMessage && (
                    <div className="workflow-step-error">
                      {step.errorMessage}
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>


      <section className="workflow-card">
        <h2>Human Approval</h2>

        <label
          htmlFor="workflow-comments"
          className="workflow-label"
        >
          Comments
        </label>

        <textarea
          id="workflow-comments"
          value={comments}
          onChange={(event) =>
            setComments(
              event.target.value
            )
          }
          placeholder="Optional comments for this decision"
          rows={4}
          disabled={
            actionLoading ||
            !canMakeDecision
          }
        />

        <div className="workflow-actions">
          <button
            type="button"
            className="workflow-button approve"
            disabled={
              actionLoading ||
              !canMakeDecision
            }
            onClick={() =>
              handleDecision("approve")
            }
          >
            {approving
              ? "Approving..."
              : "Approve"}
          </button>

          <button
            type="button"
            className="workflow-button revise"
            disabled={
              actionLoading ||
              !canMakeDecision
            }
            onClick={() =>
              handleDecision("revise")
            }
          >
            {revising
              ? "Requesting..."
              : "Revise"}
          </button>

          <button
            type="button"
            className="workflow-button reject"
            disabled={
              actionLoading ||
              !canMakeDecision
            }
            onClick={() =>
              handleDecision("reject")
            }
          >
            {rejecting
              ? "Rejecting..."
              : "Reject"}
          </button>
        </div>

        {!canMakeDecision && (
          <p className="workflow-decision-note">
            This workflow cannot receive
            another approval decision in its
            current status.
          </p>
        )}
      </section>
    </div>
  );
}