import type { CreateRequestDto } from "./requestTypes";

export interface RequestValidationErrors {
  requesterId?: string;
  organizationId?: string;
  bloodType?: string;
  unitsRequested?: string;
  urgency?: string;
  hospitalName?: string;
  latitude?: string;
  longitude?: string;
  notes?: string;
}

export const validateCreateRequest = (
  values: CreateRequestDto
): RequestValidationErrors => {
  const errors: RequestValidationErrors = {};

  if (!values.requesterId.trim()) {
    errors.requesterId = "Requester is required.";
  }

  if (!values.organizationId.trim()) {
    errors.organizationId = "Organization is required.";
  }

  if (values.unitsRequested < 1) {
    errors.unitsRequested = "Units requested must be at least 1.";
  }

  if (values.unitsRequested > 100) {
    errors.unitsRequested = "Units requested cannot exceed 100.";
  }

  if (!values.hospitalName.trim()) {
    errors.hospitalName = "Hospital name is required.";
  } else if (values.hospitalName.trim().length > 200) {
    errors.hospitalName =
      "Hospital name cannot exceed 200 characters.";
  }

  if (values.latitude < -90 || values.latitude > 90) {
    errors.latitude =
      "Latitude must be between -90 and 90.";
  }

  if (values.longitude < -180 || values.longitude > 180) {
    errors.longitude =
      "Longitude must be between -180 and 180.";
  }

  if (values.notes.length > 1000) {
    errors.notes =
      "Notes cannot exceed 1000 characters.";
  }

  return errors;
};

export const hasRequestValidationErrors = (
  errors: RequestValidationErrors
) => {
  return Object.keys(errors).length > 0;
};