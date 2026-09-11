import { createSlice, type PayloadAction } from "@reduxjs/toolkit";

interface AuthState {
  token: string | null;
  refreshToken: string | null;
  userId: string | null;
  email: string | null;
  fullName: string | null;
  role: string | null;
  /** Donor profile UUID — set after POST /api/donors/register succeeds.
   *  Different from userId: used for GET/PUT /api/donors/{id} and eligibility. */
  donorId: string | null;
  organizationId: string | null;
}

const initialState: AuthState = {
  token: localStorage.getItem("token"),
  refreshToken: localStorage.getItem("refreshToken"),
  userId: localStorage.getItem("userId"),
  email: localStorage.getItem("email"),
  fullName: localStorage.getItem("fullName"),
  role: localStorage.getItem("role"),
  donorId: localStorage.getItem("donorId"),
  organizationId: localStorage.getItem("organizationId"),
};

const authSlice = createSlice({
  name: "auth",
  initialState,
  reducers: {
    setCredentials: (
      state,
      action: PayloadAction<{
        token: string;
        refreshToken: string;
        userId: string;
        donorProfileId?: string | null;
        email: string;
        fullName: string;
        role: string;
      }>
    ) => {
      const { token, refreshToken, userId, donorProfileId, email, fullName, role } =
        action.payload;
      state.token = token;
      state.refreshToken = refreshToken;
      state.userId = userId;
      state.donorId = donorProfileId ?? null;
      state.email = email;
      state.fullName = fullName;
      state.role = role;
      state.organizationId = organizationId;

      localStorage.setItem("token", token);
      localStorage.setItem("refreshToken", refreshToken);
      localStorage.setItem("userId", userId);
      if (donorProfileId) localStorage.setItem("donorId", donorProfileId);
      else localStorage.removeItem("donorId");
      localStorage.setItem("email", email);
      localStorage.setItem("fullName", fullName);
      localStorage.setItem("role", role);
      if (organizationId) {
        localStorage.setItem("organizationId", organizationId);
      } else {
        localStorage.removeItem("organizationId");
      }
    },

    /** Persists the donor profile UUID returned by POST /api/donors/register. */
    setDonorId: (state, action: PayloadAction<string>) => {
      state.donorId = action.payload;
      localStorage.setItem("donorId", action.payload);
    },

    logout: (state) => {
      state.token = null;
      state.refreshToken = null;
      state.userId = null;
      state.email = null;
      state.fullName = null;
      state.role = null;
      state.donorId = null;
      state.organizationId = null;

      localStorage.removeItem("token");
      localStorage.removeItem("refreshToken");
      localStorage.removeItem("userId");
      localStorage.removeItem("email");
      localStorage.removeItem("fullName");
      localStorage.removeItem("role");
      localStorage.removeItem("donorId");
      localStorage.removeItem("organizationId");
    },
  },
});

export const { setCredentials, logout } = authSlice.actions;
export default authSlice.reducer;
