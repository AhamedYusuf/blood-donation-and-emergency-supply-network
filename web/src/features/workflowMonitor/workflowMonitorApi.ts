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
        "Request",
        "Workflow",
        { type: "Workflow", id },
      ],
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