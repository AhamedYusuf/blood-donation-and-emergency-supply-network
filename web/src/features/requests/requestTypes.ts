export enum BloodType {
  APositive = 0,
  ANegative = 1,
  BPositive = 2,
  BNegative = 3,
  ABPositive = 4,
  ABNegative = 5,
  OPositive = 6,
  ONegative = 7,
}

export enum RequestUrgency {
  Normal = 0,
  Urgent = 1,
  Critical = 2,
}

export enum BloodRequestStatus {
  Pending = 0,
  AwaitingApproval = 1,
  Approved = 2,
  Dispatched = 3,
  Fulfilled = 4,
  Closed = 5,
  Cancelled = 6,
}

export interface CreateRequestDto {
  requesterId: string;
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