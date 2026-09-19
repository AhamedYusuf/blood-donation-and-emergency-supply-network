import type { OrganizationFormData } from "./organizationTypes";

export type OrganizationFieldErrors = Partial<{
  name: string;
  type: string;
  address: string;
  phoneNumber: string;
  latitude: string;
  longitude: string;
}>;

export function validateOrganizationForm(
  form: OrganizationFormData,
): OrganizationFieldErrors {
  const errors: OrganizationFieldErrors = {};

  const name = form.name.trim();
  const type = form.type.trim();
  const address = form.address.trim();
  const phoneNumber = form.phoneNumber.trim();

  if (!name) errors.name = "Organization name is required.";
  else if (name.length < 2)
    errors.name = "Organization name must be at least 2 characters.";
  else if (name.length > 200)
    errors.name = "Organization name cannot exceed 200 characters.";

  if (!type) errors.type = "Please select an organization type.";

  if (!address) errors.address = "Organization address is required.";
  else if (address.length < 5)
    errors.address = "Address must be at least 5 characters.";
  else if (address.length > 500)
    errors.address = "Address cannot exceed 500 characters.";

  if (!phoneNumber) errors.phoneNumber = "Phone number is required.";
  else if (!/^\d+$/.test(phoneNumber))
    errors.phoneNumber = "Phone number must contain numbers only.";
  else if (phoneNumber.length !== 10)
    errors.phoneNumber = "Phone number must contain exactly 10 digits.";

  if (Number.isNaN(form.latitude))
    errors.latitude = "Location has not been detected yet.";
  else if (form.latitude < -90 || form.latitude > 90)
    errors.latitude = "Detected latitude is invalid.";

  if (Number.isNaN(form.longitude))
    errors.longitude = "Location has not been detected yet.";
  else if (form.longitude < -180 || form.longitude > 180)
    errors.longitude = "Detected longitude is invalid.";

  return errors;
}

export function hasOrganizationErrors(
  errors: OrganizationFieldErrors,
): boolean {
  return Object.keys(errors).length > 0;
}