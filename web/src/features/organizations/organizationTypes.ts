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
  phoneNumber: string;
}

export type UpdateOrganizationRequest = CreateOrganizationRequest;

export interface OrganizationFormData
  extends CreateOrganizationRequest {
  latitude: number;
  longitude: number;
}

export interface GeocodeOrganizationResponse {
  latitude: number;
  longitude: number;
}