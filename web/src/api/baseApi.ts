import { createApi, fetchBaseQuery } from "@reduxjs/toolkit/query/react";
import type { RootState } from "../app/store";

export const baseApi = createApi({
  reducerPath: "api",

  baseQuery: fetchBaseQuery({
    // The backend runs on 5067 in this project (see CLAUDE.md/appsettings)
    // — 5000 is just ASP.NET's generic template default, not where
    // anything here actually listens. Merging in a change that quietly
    // flipped this would break every request for anyone not explicitly
    // setting VITE_API_BASE_URL.
    baseUrl:
      import.meta.env.VITE_API_BASE_URL ??
      "http://localhost:5067/api",

    prepareHeaders: (headers, { getState }) => {
      const token = (getState() as RootState).auth.token;

      if (token) {
        headers.set("authorization", `Bearer ${token}`);
      }

      headers.set("content-type", "application/json");

      return headers;
    },
  }),

  tagTypes: [
    "Appointment",
    "Donor",
    "Inventory",
    "Organization",
    "Request",
  ],

  endpoints: () => ({}),
});