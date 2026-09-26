export type BloodType =
  | "APositive"
  | "ANegative"
  | "BPositive"
  | "BNegative"
  | "ABPositive"
  | "ABNegative"
  | "OPositive"
  | "ONegative";

export type InventoryTransactionType =
  | "DonationIn"
  | "UsageOut"
  | "TransferIn"
  | "TransferOut";

export type RequestUrgency =
  | "Normal"
  | "Urgent"
  | "Critical";

/*
 * Backend enums can be returned either as:
 *
 *   "OPositive"
 *
 * or:
 *
 *   6
 *
 * The frontend therefore accepts both representations.
 */
export type ApiEnumValue<T extends string> = T | number;

export interface InventoryResponse {
  id: string;
  organizationId: string;
  bloodType: ApiEnumValue<BloodType>;
  unitsAvailable: number;
  lowStockThreshold: number;
  isLowStock: boolean;
  lastUpdated: string;
}

export interface AdjustInventoryRequest {
  units: number;
  transactionType: InventoryTransactionType;
  relatedAppointmentId?: string | null;
  relatedTransferOrgId?: string | null;
}

export interface InventoryTransactionResponse {
  id: string;
  inventoryId: string;
  bloodType: ApiEnumValue<BloodType>;
  transactionType: ApiEnumValue<InventoryTransactionType>;
  units: number;
  relatedAppointmentId?: string | null;
  relatedTransferOrgId?: string | null;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/*
 * Authenticated frontend Stock Check request.
 *
 * This is used by:
 * POST /api/inventory/stock-check
 */
export interface StockCheckRequest {
  organizationId: string;
  bloodType: BloodType;
  requiredUnits: number;
}

export interface StockCheckResponse {
  organizationId: string;
  bloodType: ApiEnumValue<BloodType>;
  requiredUnits: number;
  availableUnits: number;
  remainingUnits: number;
  sufficient: boolean;
  lowStock: boolean;
}

/*
 * Richer stock-check result that also lists nearby organizations with
 * spare stock, for the Emergency & Risk screen's transfer-candidate
 * panel. Backed by POST /api/inventory/stock-check/candidates — a
 * normal, JWT-authenticated, org-scoped route, NOT the agent-service's
 * own /agents/stock-check (unauthenticated) or the .NET internal
 * endpoint (requires the server-to-server secret the browser can't hold).
 */
export interface StockCheckCandidateTransferOrg {
  organizationId: string;
  distanceKm: number;
  unitsAvailable: number;
}

export interface StockCheckWithCandidatesResponse {
  sufficient: boolean;
  ownStockUnits: number;
  shortfallUnits: number;
  candidateTransferOrgs: StockCheckCandidateTransferOrg[];
}

/*
 * Stock Risk Agent response.
 */
export interface StockRiskResponse {
  organizationId: string;
  riskLevel: string;
  totalUnits: number;
  lowStockTypes: number;
  atRiskInventory: InventoryResponse[];
  recommendation: string;
}

/*
 * Emergency Recommendation request.
 */
export interface EmergencyInventoryRequest {
  bloodType: BloodType;
  requiredUnits: number;
  urgency: RequestUrgency;
}

/*
 * Emergency Recommendation Agent response.
 *
 * New contract:
 * - shortfallUnits instead of shortfall
 * - urgency instead of priority
 */
export interface EmergencyInventoryRecommendation {
  bloodType: ApiEnumValue<BloodType>;
  requiredUnits: number;
  availableUnits: number;
  shortfallUnits: number;
  recommendation: string;
  suggestedAction: string;
  urgency: RequestUrgency | number;
}