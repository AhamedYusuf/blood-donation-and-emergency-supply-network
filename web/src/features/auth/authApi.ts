import { baseApi } from "../../api/baseApi";

// ── Request shapes (mirror backend DTOs exactly) ──────────────────────────────

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  phoneNumber: string;
  /** Hardcoded to "donor" from the public registration form */
  role: string;
  organizationId?: string | null;
}

// ── Response shape ────────────────────────────────────────────────────────────

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  userId: string;
  /** Donor profile UUID, or null until the donor completes profile setup. */
  donorProfileId: string | null;
  email: string;
  fullName: string;
  role: string;
  organizationId: string | null;
}

// ── API slice ─────────────────────────────────────────────────────────────────

export const authApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    login: builder.mutation<AuthResponse, LoginRequest>({
      query: (credentials) => ({
        url: "/auth/login",
        method: "POST",
        body: credentials,
      }),
    }),

    register: builder.mutation<AuthResponse, RegisterRequest>({
      query: (body) => ({
        url: "/auth/register",
        method: "POST",
        body,
      }),
    }),

    refresh: builder.mutation<AuthResponse, string>({
      query: (refreshToken) => ({
        url: "/auth/refresh",
        method: "POST",
        body: refreshToken,
      }),
    }),
  }),
});

export const { useLoginMutation, useRegisterMutation, useRefreshMutation } =
  authApi;