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

export type RequestUrgency = "Normal" | "Urgent" | "Critical";

/*
 * The backend uses C# enums.
 * Depending on JSON serialization, the API can return either:
 *
 *   "OPositive"
 *
 * or:
 *
 *   6
 *
 * Therefore the frontend accepts both formats.
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

export interface StockRiskResponse {
  organizationId: string;
  riskLevel: string;
  totalUnits: number;
  lowStockTypes: number;
  atRiskInventory: InventoryResponse[];
  recommendation: string;
}

export interface EmergencyInventoryRequest {
  bloodType: BloodType;
  requiredUnits: number;
  urgency: RequestUrgency;
}

export interface EmergencyInventoryRecommendation {
  bloodType: ApiEnumValue<BloodType>;
  requiredUnits: number;
  availableUnits: number;
  shortfall: number;
  recommendation: string;
  suggestedAction: string;
  priority: RequestUrgency | number;
}
