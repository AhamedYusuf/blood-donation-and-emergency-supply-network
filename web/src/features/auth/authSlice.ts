import { createSlice, type PayloadAction } from "@reduxjs/toolkit";

interface AuthState {
  token: string | null;
  userId: string | null;
  email: string | null;
  role: string | null;
  organizationId: string | null;
}

const initialState: AuthState = {
  token: localStorage.getItem("token"),
  userId: localStorage.getItem("userId"),
  email: localStorage.getItem("email"),
  role: localStorage.getItem("role"),
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
        userId: string;
        email: string;
        role: string;
        organizationId: string | null;
      }>
    ) => {
      const { token, userId, email, role, organizationId } = action.payload;
      state.token = token;
      state.userId = userId;
      state.email = email;
      state.role = role;
      state.organizationId = organizationId;

      localStorage.setItem("token", token);
      localStorage.setItem("userId", userId);
      localStorage.setItem("email", email);
      localStorage.setItem("role", role);
      if (organizationId) {
        localStorage.setItem("organizationId", organizationId);
      } else {
        localStorage.removeItem("organizationId");
      }
    },
    logout: (state) => {
      state.token = null;
      state.userId = null;
      state.email = null;
      state.role = null;
      state.organizationId = null;

      localStorage.removeItem("token");
      localStorage.removeItem("userId");
      localStorage.removeItem("email");
      localStorage.removeItem("role");
      localStorage.removeItem("organizationId");
    },
  },
});

export const { setCredentials, logout } = authSlice.actions;
export default authSlice.reducer;
