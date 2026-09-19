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
    }),

    getLatestWorkflowByBloodRequest: builder.query<
      AgentWorkflow,
      string
    >({
      query: (bloodRequestId) =>
        `/agent/workflows/request/${bloodRequestId}`,
    }),

    getWorkflowSteps: builder.query<
      AgentStep[],
      string
    >({
      query: (id) =>
        `/agent/workflows/${id}/steps`,
    }),

    getWorkflowSummary: builder.query<
      WorkflowSummary,
      string
    >({
      query: (id) =>
        `/agent/workflows/${id}/summary`,
    }),

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
    }),

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