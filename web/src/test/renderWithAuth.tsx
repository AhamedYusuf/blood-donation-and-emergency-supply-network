import type { ReactElement } from "react";
import { configureStore } from "@reduxjs/toolkit";
import { Provider } from "react-redux";
import { MemoryRouter } from "react-router-dom";
import { render } from "@testing-library/react";
import { baseApi } from "../api/baseApi";
import authReducer from "../features/auth/authSlice";

interface PartialAuthState {
  token?: string | null;
  role?: string | null;
  donorId?: string | null;
  organizationId?: string | null;
}

// Builds a real Redux store (same reducers as the app) preloaded with just
// the auth fields a guard test cares about, so guards are exercised against
// the actual authSlice shape rather than a hand-rolled fake.
export function renderWithAuth(ui: ReactElement, auth: PartialAuthState = {}, initialPath = "/") {
  const store = configureStore({
    reducer: {
      auth: authReducer,
      [baseApi.reducerPath]: baseApi.reducer,
    },
    middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(baseApi.middleware),
    preloadedState: {
      auth: {
        token: auth.token ?? null,
        refreshToken: null,
        userId: null,
        email: null,
        fullName: null,
        role: auth.role ?? null,
        donorId: auth.donorId ?? null,
        organizationId: auth.organizationId ?? null,
      },
    },
  });

  return render(
    <Provider store={store}>
      <MemoryRouter initialEntries={[initialPath]}>{ui}</MemoryRouter>
    </Provider>
  );
}
