import { baseApi } from "../../api/baseApi";
import type {
  CreateOrganizationRequest,
  Organization,
  UpdateOrganizationRequest,
} from "./organizationTypes";

export const organizationsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getOrganizations: builder.query<Organization[], void>({
      query: () => "/organizations",
      providesTags: ["Organization"],
    }),

    getOrganization: builder.query<Organization, string>({
      query: (id) => `/organizations/${id}`,
      providesTags: ["Organization"],
    }),

    createOrganization: builder.mutation<
      Organization,
      CreateOrganizationRequest
    >({
      query: (body) => ({
        url: "/organizations",
        method: "POST",
        body,
      }),
      invalidatesTags: ["Organization"],
    }),

    updateOrganization: builder.mutation<
      Organization,
      {
        id: string;
        body: UpdateOrganizationRequest;
      }
    >({
      query: ({ id, body }) => ({
        url: `/organizations/${id}`,
        method: "PUT",
        body,
      }),
      invalidatesTags: ["Organization"],
    }),

    deleteOrganization: builder.mutation<void, string>({
      query: (id) => ({
        url: `/organizations/${id}`,
        method: "DELETE",
      }),
      invalidatesTags: ["Organization"],
    }),
  }),
});

export const {
  useGetOrganizationsQuery,
  useGetOrganizationQuery,
  useCreateOrganizationMutation,
  useUpdateOrganizationMutation,
  useDeleteOrganizationMutation,
} = organizationsApi;