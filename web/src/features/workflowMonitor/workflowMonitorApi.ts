import { baseApi } from "../../api/baseApi";

import type {
  AgentStep,
  AgentWorkflow,
  WorkflowDecisionRequest,
  WorkflowSummary,
} from "./workflowMonitorTypes";

export const workflowMonitorApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getWorkflow: builder.query<
      AgentWorkflow,
      string
    >({
      query: (id) => `/agent/workflows/${id}`,
      providesTags: (_result, _error, id) => [
        "Workflow",
        { type: "Workflow", id },
      ],
    }),

    getLatestWorkflowByBloodRequest: builder.query<
      AgentWorkflow,
      string
    >({
      query: (bloodRequestId) =>
        `/agent/workflows/request/${bloodRequestId}`,
      providesTags: ["Workflow"],
    }),

    getWorkflowSteps: builder.query<
      AgentStep[],
      string
    >({
      query: (id) =>
        `/agent/workflows/${id}/steps`,
      providesTags: (_result, _error, id) => [
        "Workflow",
        { type: "Workflow", id },
      ],
    }),

    getWorkflowSummary: builder.query<
      WorkflowSummary,
      string
    >({
      query: (id) =>
        `/agent/workflows/${id}/summary`,
      providesTags: (_result, _error, id) => [
        "Workflow",
        { type: "Workflow", id },
      ],
    }),

    // approveWorkflow's HTTP response doesn't come back until the backend
    // has synchronously awaited the whole Python coordinator resume cycle
    // (AgentWorkflowsController.Approve awaits ResumePythonWorkflowAsync,
    // which awaits POST /resume-workflow — confirmed live: by the time
    // this call resolves, a sufficient-stock approval has already
    // deducted real inventory and an insufficient-stock one has already
    // created real appointments). So invalidating these tags on success
    // is safe and accurate, not premature — previously this mutation had
    // no invalidatesTags at all, so e.g. the Inventory page's stock count
    // only ever picked up a post-approval deduction on a full page
    // refresh, never from the live app.
    approveWorkflow: builder.mutation<
      AgentWorkflow,
      {
        id: string;
        body: WorkflowDecisionRequest;
      }
    >({
      query: ({ id, body }) => ({
        url: `/agent/workflows/${id}/approve`,
        method: "POST",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        "Inventory",
        "Request",
        "Appointment",
        "Workflow",
        { type: "Workflow", id },
      ],
    }),

    // Rejecting a workflow now also cancels its BloodRequest server-side
    // (see AgentWorkflowService.RejectAsync) — invalidate "Request" so
    // any open Requests list/detail view picks that up live too.
    rejectWorkflow: builder.mutation<
      AgentWorkflow,
      {
        id: string;
        body: WorkflowDecisionRequest;
      }
    >({
      query: ({ id, body }) => ({
        url: `/agent/workflows/${id}/reject`,
        method: "POST",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        "Request",
        "Workflow",
        { type: "Workflow", id },
      ],
    }),

    reviseWorkflow: builder.mutation<
      AgentWorkflow,
      {
        id: string;
        body: WorkflowDecisionRequest;
      }
    >({
      query: ({ id, body }) => ({
        url: `/agent/workflows/${id}/revise`,
        method: "POST",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        "Request",
        "Workflow",
        { type: "Workflow", id },
      ],
    }),
  }),
});

export const {
  useGetWorkflowQuery,
  useGetLatestWorkflowByBloodRequestQuery,
  useGetWorkflowStepsQuery,
  useGetWorkflowSummaryQuery,
  useApproveWorkflowMutation,
  useRejectWorkflowMutation,
  useReviseWorkflowMutation,
} = workflowMonitorApi;