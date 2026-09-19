export enum BloodType {
  APositive = "A+",
  ANegative = "A-",
  BPositive = "B+",
  BNegative = "B-",
  ABPositive = "AB+",
  ABNegative = "AB-",
  OPositive = "O+",
  ONegative = "O-",
}

export enum RequestUrgency {
  Normal = "normal",
  Urgent = "urgent",
  Critical = "critical",
}

export enum BloodRequestStatus {
  Open = "open",
  Matching = "matching",
  AwaitingApproval = "awaiting_approval",
  DonorsNotified = "donors_notified",
  PartiallyFulfilled = "partially_fulfilled",
  Fulfilled = "fulfilled",
  Expired = "expired",
  Cancelled = "cancelled",
}

export interface CreateRequestDto {
  organizationId: string;

  bloodType: BloodType;

  unitsRequested: number;

  urgency: RequestUrgency;

  hospitalName: string;

  latitude: number;

  longitude: number;

  notes: string;
}

export interface RequestResponseDto {
  id: string;

  requesterId: string;

  organizationId: string;

  bloodType: BloodType;

  unitsRequested: number;

  urgency: RequestUrgency;

  status: BloodRequestStatus;

  hospitalName: string;

  latitude: number;

  longitude: number;

  notes: string;

  createdAt: string;

  fulfilledAt: string | null;

  closedAt: string | null;
}

export interface RequestStatusUpdateDto {
  status: BloodRequestStatus;
}

export interface RequestFilters {
  bloodType?: BloodType;

  urgency?: RequestUrgency;

  status?: BloodRequestStatus;

  organizationId?: string;

  page?: number;

  pageSize?: number;

  sortBy?: string;

  descending?: boolean;
}