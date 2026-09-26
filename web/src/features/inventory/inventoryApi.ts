import { baseApi } from "../../api/baseApi";
import type {
  AdjustInventoryRequest,
  BloodType,
  EmergencyInventoryRecommendation,
  EmergencyInventoryRequest,
  InventoryResponse,
  InventoryTransactionResponse,
  InventoryTransactionType,
  PagedResult,
  StockCheckRequest,
  StockCheckResponse,
  StockCheckWithCandidatesResponse,
  StockRiskResponse,
} from "./inventoryTypes";

const BLOOD_TYPE_TO_NUMBER: Record<BloodType, number> = {
  APositive: 0,
  ANegative: 1,
  BPositive: 2,
  BNegative: 3,
  ABPositive: 4,
  ABNegative: 5,
  OPositive: 6,
  ONegative: 7,
};

const TRANSACTION_TYPE_TO_NUMBER: Record<
  InventoryTransactionType,
  number
> = {
  DonationIn: 0,
  UsageOut: 1,
  TransferIn: 2,
  TransferOut: 3,
};

const URGENCY_TO_NUMBER: Record<
  EmergencyInventoryRequest["urgency"],
  number
> = {
  Normal: 0,
  Urgent: 1,
  Critical: 2,
};

export const inventoryApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // =====================================================
    // INVENTORY
    // =====================================================

    getInventory: builder.query<InventoryResponse[], string>({
      query: (organizationId) =>
        `/inventory/${organizationId}`,
      providesTags: ["Inventory"],
    }),

    getLowStock: builder.query<InventoryResponse[], string>({
      query: (organizationId) =>
        `/inventory/low-stock?organizationId=${organizationId}`,
      providesTags: ["Inventory"],
    }),

    // =====================================================
    // TRANSACTIONS
    // =====================================================

    getTransactions: builder.query<
      PagedResult<InventoryTransactionResponse>,
      {
        organizationId: string;
        page?: number;
        pageSize?: number;
      }
    >({
      query: ({
        organizationId,
        page = 1,
        pageSize = 20,
      }) =>
        `/inventory/transactions/${organizationId}?page=${page}&pageSize=${pageSize}`,
      providesTags: ["Inventory"],
    }),

    adjustInventory: builder.mutation<
      InventoryResponse,
      {
        inventoryId: string;
        request: AdjustInventoryRequest;
      }
    >({
      query: ({
        inventoryId,
        request,
      }) => ({
        url: `/inventory/${inventoryId}/adjust`,
        method: "PUT",
        body: {
          ...request,
          transactionType:
            TRANSACTION_TYPE_TO_NUMBER[
              request.transactionType
            ],
        },
      }),
      invalidatesTags: ["Inventory"],
    }),

    createTransaction: builder.mutation<
      InventoryTransactionResponse,
      {
        organizationId: string;
        bloodType: BloodType;
        units: number;
        transactionType: InventoryTransactionType;
        relatedAppointmentId?: string | null;
        relatedTransferOrgId?: string | null;
      }
    >({
      query: ({
        organizationId,
        bloodType,
        units,
        transactionType,
        relatedAppointmentId,
        relatedTransferOrgId,
      }) => ({
        url: "/inventory/transactions",
        method: "POST",
        body: {
          organizationId,
          bloodType:
            BLOOD_TYPE_TO_NUMBER[bloodType],
          units,
          transactionType:
            TRANSACTION_TYPE_TO_NUMBER[
              transactionType
            ],
          relatedAppointmentId:
            relatedAppointmentId ?? null,
          relatedTransferOrgId:
            relatedTransferOrgId ?? null,
        },
      }),
      invalidatesTags: ["Inventory"],
    }),

    // =====================================================
    // AGENT 01 - STOCK CHECK
    //
    // Browser-facing authenticated endpoint.
    //
    // IMPORTANT:
    // Do NOT use /internal/agent/check-stock here.
    // The internal endpoint requires X-Internal-Secret.
    // =====================================================

    checkStock: builder.mutation<
      StockCheckResponse,
      StockCheckRequest
    >({
      query: (request) => ({
        url: "/inventory/stock-check",
        method: "POST",
        body: {
          organizationId: request.organizationId,
          bloodType:
            BLOOD_TYPE_TO_NUMBER[request.bloodType],
          requiredUnits: request.requiredUnits,
        },
      }),
    }),

    // =====================================================
    // AGENT 01 - STOCK CHECK WITH TRANSFER CANDIDATES
    //
    // Same auth/org-scoping as checkStock above, but also returns
    // nearby organizations with spare stock (for the Emergency & Risk
    // screen's transfer-candidate panel). Do NOT call the agent-service
    // directly for this from the browser — see agentApi.ts's removal.
    // =====================================================

    checkStockWithCandidates: builder.mutation<
      StockCheckWithCandidatesResponse,
      StockCheckRequest
    >({
      query: (request) => ({
        url: "/inventory/stock-check/candidates",
        method: "POST",
        body: {
          organizationId: request.organizationId,
          bloodType:
            BLOOD_TYPE_TO_NUMBER[request.bloodType],
          requiredUnits: request.requiredUnits,
        },
      }),
    }),

    // =====================================================
    // AGENT 02 - STOCK RISK
    // =====================================================

    getStockRisk: builder.query<
      StockRiskResponse,
      string
    >({
      query: (organizationId) =>
        `/inventory/stock-risk/${organizationId}`,
      providesTags: ["Inventory"],
    }),

    // =====================================================
    // AGENT 03 - EMERGENCY RECOMMENDATION
    //
    // Browser-facing authenticated endpoint.
    //
    // IMPORTANT:
    // Do NOT use /internal/agent/inventory-recommendation
    // from the browser because that endpoint requires
    // the internal server-to-server secret.
    // =====================================================

    getEmergencyRecommendation: builder.mutation<
      EmergencyInventoryRecommendation,
      EmergencyInventoryRequest
    >({
      query: (request) => ({
        url: "/inventory/emergency-recommendation",
        method: "POST",
        body: {
          bloodType:
            BLOOD_TYPE_TO_NUMBER[request.bloodType],
          requiredUnits: request.requiredUnits,
          urgency:
            URGENCY_TO_NUMBER[request.urgency],
        },
      }),
    }),
  }),
});

export const {
  useGetInventoryQuery,
  useGetLowStockQuery,
  useGetTransactionsQuery,
  useAdjustInventoryMutation,
  useCreateTransactionMutation,
  useCheckStockMutation,
  useCheckStockWithCandidatesMutation,
  useGetStockRiskQuery,
  useGetEmergencyRecommendationMutation,
} = inventoryApi;