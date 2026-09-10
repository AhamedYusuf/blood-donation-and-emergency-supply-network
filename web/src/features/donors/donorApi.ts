import { baseApi } from "../../api/baseApi";

// ── Request / Response shapes (mirror backend DTOs exactly) ───────────────────

export type BloodType =
  | "A+"
  | "A-"
  | "B+"
  | "B-"
  | "AB+"
  | "AB-"
  | "O+"
  | "O-";

export const BLOOD_TYPE_OPTIONS = (
  ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"] as BloodType[]
).map((bt) => ({ value: bt, label: bt }));

export interface DonorRegisterRequest {
  bloodType: string;
  /** ISO date string: "YYYY-MM-DD" — serialised from DateOnly on backend */
  dateOfBirth: string;
  address?: string;
  /**
   * Keys MUST be snake_case matching EligibilityRuleEngine.cs TryGetValue calls:
   * recent_illness | recent_surgery | chronic_condition | hiv_positive | hepatitis
   */
  medicalFlags?: Record<string, boolean>;
}

export interface DonorUpdateRequest {
  address?: string;
  medicalFlags?: Record<string, boolean>;
  /** ISO date string: "YYYY-MM-DD" */
  lastDonationDate?: string;
}

export interface DonorProfileResponse {
  id: string;
  userId: string;
  bloodType: string;
  /** 'eligible' | 'not_eligible' | 'pending_review' */
  eligibilityStatus: string;
  dateOfBirth: string;
  lastDonationDate?: string | null;
  address?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  locationVerified: boolean;
  verifiedByAdmin: boolean;
}

export interface EligibilityResponse {
  isEligible: boolean;
  reason?: string | null;
  daysUntilEligible?: number | null;
}

export interface DonorSearchParams {
  bloodType?: string;
  lat?: number;
  lng?: number;
  radiusKm?: number;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// ── API slice ─────────────────────────────────────────────────────────────────

export const donorApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    /** POST /api/donors/register — donor (self, post-auth) */
    registerDonorProfile: builder.mutation<
      DonorProfileResponse,
      DonorRegisterRequest
    >({
      query: (body) => ({
        url: "/donors/register",
        method: "POST",
        body,
      }),
      invalidatesTags: ["Donor"],
    }),

    /** GET /api/donors/{id} — donor (self) / staff / admin */
    getDonorProfile: builder.query<DonorProfileResponse, string>({
      query: (id) => `/donors/${id}`,
      providesTags: (_result, _error, id) => [{ type: "Donor", id }],
    }),

    /** PUT /api/donors/{id} — donor (self) / admin */
    updateDonorProfile: builder.mutation<
      DonorProfileResponse,
      { id: string; body: DonorUpdateRequest }
    >({
      query: ({ id, body }) => ({
        url: `/donors/${id}`,
        method: "PUT",
        body,
      }),
      invalidatesTags: (_result, _error, { id }) => [{ type: "Donor", id }],
    }),

    /**
     * GET /api/donors/search — staff / admin only
     * Note: lat/lng/radiusKm filtering is stored but not yet evaluated
     * server-side (pending Haversine helper from Student 3).
     */
    searchDonors: builder.query<
      PagedResult<DonorProfileResponse>,
      DonorSearchParams
    >({
      query: ({ bloodType, lat, lng, radiusKm, page = 1, pageSize = 20 }) => {
        const params = new URLSearchParams();
        if (bloodType) params.set("bloodType", bloodType);
        if (lat != null) params.set("lat", String(lat));
        if (lng != null) params.set("lng", String(lng));
        if (radiusKm != null) params.set("radiusKm", String(radiusKm));
        params.set("page", String(page));
        params.set("pageSize", String(pageSize));
        return `/donors/search?${params.toString()}`;
      },
      providesTags: (result) =>
        result
          ? [
              ...result.items.map((d) => ({ type: "Donor" as const, id: d.id })),
              { type: "Donor" as const, id: "LIST" },
            ]
          : [{ type: "Donor" as const, id: "LIST" }],
    }),

    /**
     * POST /api/donors/{id}/verify — admin only
     * Sets verifiedByAdmin = true; returns 204 No Content.
     */
    verifyDonor: builder.mutation<void, string>({
      query: (id) => ({
        url: `/donors/${id}/verify`,
        method: "POST",
      }),
      invalidatesTags: (_result, _error, id) => [
        { type: "Donor", id },
        { type: "Donor", id: "LIST" },
      ],
    }),

    /** GET /api/donors/{id}/eligibility — donor (self) / staff / admin */
    getDonorEligibility: builder.query<EligibilityResponse, string>({
      query: (id) => `/donors/${id}/eligibility`,
    }),
  }),
});

export const {
  useRegisterDonorProfileMutation,
  useGetDonorProfileQuery,
  useUpdateDonorProfileMutation,
  useSearchDonorsQuery,
  useVerifyDonorMutation,
  useGetDonorEligibilityQuery,
} = donorApi;
