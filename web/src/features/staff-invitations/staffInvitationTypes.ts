export interface StaffInvitation {
  id: string;
  email: string;
  organizationId: string;
  organizationName: string;
  token: string;
  expiresAt: string;
  createdAt: string;
  acceptedAt: string | null;
}

export interface CreateStaffInvitationRequest {
  email: string;
  organizationId: string;
}