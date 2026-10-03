import { baseApi } from "../../api/baseApi";

export interface AgentMatch {
  rank: number;
  distanceKm: number;
  reliabilityScore: number;
}

export interface Appointment {
  id: string;
  donorId: string;
  organizationId: string;
  relatedWorkflowId: string | null;
  scheduledTime: string;
  status: string;
  donorBloodType: string | null;
  // Populated only when relatedWorkflowId is set and the Matching &
  // Dispatch Agent's own search_donors step data is available — see
  // AppointmentService.GetAgentMatchesAsync on the backend.
  agentMatch: AgentMatch | null;
  unitsDonated: number | null;
  createdAt: string;
  updatedAt: string;
}

interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

type UpcomingResult = PagedResult<Appointment>;
type UpcomingArgs = { orgId: string; page?: number; pageSize?: number };
type DonorAppointmentsArgs = { donorId: string };
type CompleteArgs = { id: string; unitsDonated: number };
type UpdateStatusArgs = { id: string; newStatus: string };
type BookArgs = { organizationId: string; scheduledTime: string; relatedWorkflowId?: string | null };
type RescheduleArgs = { id: string; newScheduledTime: string };

export const appointmentsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getUpcomingByOrganization: builder.query<UpcomingResult, UpcomingArgs>({
      query: ({ orgId, page = 1, pageSize = 20 }) =>
        `/appointments/bloodbank/${orgId}/upcoming?page=${page}&pageSize=${pageSize}`,
      providesTags: ["Appointment"],
    }),
    getMyAppointments: builder.query<Appointment[], DonorAppointmentsArgs>({
      query: ({ donorId }) => `/appointments/donor/${donorId}`,
      providesTags: ["Appointment"],
    }),
    completeAppointment: builder.mutation<Appointment, CompleteArgs>({
      query: ({ id, unitsDonated }) => ({
        url: `/appointments/${id}/complete`,
        method: "POST",
        body: { unitsDonated },
      }),
      invalidatesTags: ["Appointment"],
    }),
    updateAppointmentStatus: builder.mutation<Appointment, UpdateStatusArgs>({
      query: ({ id, newStatus }) => ({
        url: `/appointments/${id}/status`,
        method: "PUT",
        body: { newStatus },
      }),
      invalidatesTags: ["Appointment"],
    }),

    // ── Donor-facing (mirrors the mobile app's appointments_repository.dart) ──

    getByDonor: builder.query<Appointment[], string>({
      query: (donorId) => `/appointments/donor/${donorId}`,
      providesTags: ["Appointment"],
    }),
    bookAppointment: builder.mutation<Appointment, BookArgs>({
      query: ({ organizationId, scheduledTime, relatedWorkflowId = null }) => ({
        url: "/appointments",
        method: "POST",
        body: { organizationId, scheduledTime, relatedWorkflowId },
      }),
      invalidatesTags: ["Appointment"],
    }),
    confirmAppointment: builder.mutation<Appointment, string>({
      query: (id) => ({ url: `/appointments/${id}/confirm`, method: "POST" }),
      invalidatesTags: ["Appointment"],
    }),
    declineAppointment: builder.mutation<Appointment, string>({
      query: (id) => ({ url: `/appointments/${id}/decline`, method: "POST" }),
      invalidatesTags: ["Appointment"],
    }),
    rescheduleAppointment: builder.mutation<Appointment, RescheduleArgs>({
      query: ({ id, newScheduledTime }) => ({
        url: `/appointments/${id}/reschedule`,
        method: "PUT",
        body: { newScheduledTime },
      }),
      invalidatesTags: ["Appointment"],
    }),
  }),
});

export const {
  useGetUpcomingByOrganizationQuery,
  useGetMyAppointmentsQuery,
  useCompleteAppointmentMutation,
  useUpdateAppointmentStatusMutation,
  useGetByDonorQuery,
  useBookAppointmentMutation,
  useConfirmAppointmentMutation,
  useDeclineAppointmentMutation,
  useRescheduleAppointmentMutation,
} = appointmentsApi;