import { baseApi } from "../../api/baseApi";

export interface Appointment {
  id: string;
  donorId: string;
  organizationId: string;
  relatedWorkflowId: string | null;
  scheduledTime: string;
  status: string;
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
type CompleteArgs = { id: string; unitsDonated: number };
type UpdateStatusArgs = { id: string; newStatus: string };

export const appointmentsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getUpcomingByOrganization: builder.query<UpcomingResult, UpcomingArgs>({
      query: ({ orgId, page = 1, pageSize = 20 }) =>
        `/appointments/bloodbank/${orgId}/upcoming?page=${page}&pageSize=${pageSize}`,
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
  }),
});

export const {
  useGetUpcomingByOrganizationQuery,
  useCompleteAppointmentMutation,
  useUpdateAppointmentStatusMutation,
} = appointmentsApi;