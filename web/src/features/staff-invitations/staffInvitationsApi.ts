import { baseApi } from "../../api/baseApi";
import type {
  CreateStaffInvitationRequest,
  StaffInvitation,
} from "./staffInvitationTypes";

export const staffInvitationsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    createStaffInvitation: builder.mutation<
      StaffInvitation,
      CreateStaffInvitationRequest
    >({
      query: (body) => ({
        url: "/staff-invitations",
        method: "POST",
        body,
      }),
      invalidatesTags: ["StaffInvitation"],
    }),

    getStaffInvitations: builder.query<StaffInvitation[], string>({
      query: (organizationId) => ({
        url: "/staff-invitations",
        params: { organizationId },
      }),
      providesTags: ["StaffInvitation"],
    }),
  }),
});

export const {
  useCreateStaffInvitationMutation,
  useGetStaffInvitationsQuery,
} = staffInvitationsApi;