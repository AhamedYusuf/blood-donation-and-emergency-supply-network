export interface Organization {
  id: string;
  name: string;
  type: string;
  address: string;
  latitude: number;
  longitude: number;
  phoneNumber: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateOrganizationRequest {
  name: string;
  type: string;
  address: string;
  latitude: number;
  longitude: number;
  phoneNumber: string;
}

export type UpdateOrganizationRequest = CreateOrganizationRequest;

export type OrganizationFormData = CreateOrganizationRequest;