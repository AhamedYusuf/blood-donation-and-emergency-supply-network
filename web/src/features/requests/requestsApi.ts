import { baseApi } from "../../api/baseApi";

import type {
  CreateRequestDto,
  RequestFilters,
  RequestResponseDto,
  RequestStatusUpdateDto,
} from "./requestTypes";

export const requestsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getRequests: builder.query<
      RequestResponseDto[],
      RequestFilters | undefined
    >({
      query: (filters) => {
        const safeFilters = filters ?? {};
        const params = new URLSearchParams();

        if (safeFilters.bloodType !== undefined) {
          params.append(
            "bloodType",
            String(safeFilters.bloodType)
          );
        }

        if (safeFilters.urgency !== undefined) {
          params.append(
            "urgency",
            String(safeFilters.urgency)
          );
        }

        if (safeFilters.status !== undefined) {
          params.append(
            "status",
            String(safeFilters.status)
          );
        }

        if (safeFilters.organizationId) {
          params.append(
            "organizationId",
            safeFilters.organizationId
          );
        }

        params.append(
          "page",
          String(safeFilters.page ?? 1)
        );

        params.append(
          "pageSize",
          String(safeFilters.pageSize ?? 10)
        );

        params.append(
          "sortBy",
          safeFilters.sortBy ?? "createdAt"
        );

        params.append(
          "descending",
          String(safeFilters.descending ?? true)
        );

        return `/requests?${params.toString()}`;
      },

      providesTags: ["Request"],
    }),

    getRequestById: builder.query<
      RequestResponseDto,
      string
    >({
      query: (id) => `/requests/${id}`,

      providesTags: (_result, _error, id) => [
        {
          type: "Request",
          id,
        },
      ],
    }),

    createRequest: builder.mutation<
      RequestResponseDto,
      CreateRequestDto
    >({
      query: (body) => ({
        url: "/requests",
        method: "POST",
        body,
      }),

      invalidatesTags: ["Request"],
    }),

    updateRequestStatus: builder.mutation<
      RequestResponseDto,
      {
        id: string;
        body: RequestStatusUpdateDto;
      }
    >({
      query: ({ id, body }) => ({
        url: `/requests/${id}/status`,
        method: "PUT",
        body,
      }),

      invalidatesTags: (_result, _error, { id }) => [
        "Request",
        {
          type: "Request",
          id,
        },
      ],
    }),

    closeRequest: builder.mutation<
      RequestResponseDto,
      string
    >({
      query: (id) => ({
        url: `/requests/${id}/close`,
        method: "POST",
      }),

      invalidatesTags: (_result, _error, id) => [
        "Request",
        {
          type: "Request",
          id,
        },
      ],
    }),

    deleteRequest: builder.mutation<void, string>({
      query: (id) => ({
        url: `/requests/${id}`,
        method: "DELETE",
      }),

      invalidatesTags: ["Request"],
    }),
  }),
});

export const {
  useGetRequestsQuery,
  useGetRequestByIdQuery,
  useCreateRequestMutation,
  useUpdateRequestStatusMutation,
  useCloseRequestMutation,
  useDeleteRequestMutation,
} = requestsApi;