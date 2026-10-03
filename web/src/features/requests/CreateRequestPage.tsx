import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useSelector } from "react-redux";

import type { RootState } from "../../app/store";
import { getApiErrorMessage } from "../../api/getApiErrorMessage";

import { useGetCurrentUserQuery } from "../auth/authApi";
import { useGetOrganizationsQuery } from "../organizations/organizationsApi";

import { useCreateRequestMutation } from "./requestsApi";

import {
  BloodType,
  RequestUrgency,
  type CreateRequestDto,
} from "./requestTypes";

import {
  hasRequestValidationErrors,
  validateCreateRequest,
  type RequestValidationErrors,
} from "./requestValidation";

import "./requests.css";

const bloodTypeOptions = [
  { value: BloodType.APositive, label: "A+" },
  { value: BloodType.ANegative, label: "A-" },
  { value: BloodType.BPositive, label: "B+" },
  { value: BloodType.BNegative, label: "B-" },
  { value: BloodType.ABPositive, label: "AB+" },
  { value: BloodType.ABNegative, label: "AB-" },
  { value: BloodType.OPositive, label: "O+" },
  { value: BloodType.ONegative, label: "O-" },
];

const urgencyOptions = [
  {
    value: RequestUrgency.Normal,
    label: "Normal",
  },
  {
    value: RequestUrgency.Urgent,
    label: "Urgent",
  },
  {
    value: RequestUrgency.Critical,
    label: "Critical",
  },
];

export default function CreateRequestPage() {
  const navigate = useNavigate();

  const userId = useSelector(
    (state: RootState) => state.auth.userId
  );

  const userOrganizationId = useSelector(
    (state: RootState) => state.auth.organizationId
  );

  const role = useSelector(
    (state: RootState) => state.auth.role
  );

  const isStaff = (role ?? "").toLowerCase() === "staff";

  const {
    data: currentUser,
    isFetching: currentUserLoading,
    isError: currentUserError,
  } = useGetCurrentUserQuery(undefined, {
    skip: !isStaff,
    refetchOnMountOrArgChange: true,
  });

  const {
    data: organizations = [],
    isLoading: organizationsLoading,
    isError: organizationsError,
  } = useGetOrganizationsQuery();

  const [
    createRequest,
    {
      isLoading: isCreating,
      isError: createError,
    },
  ] = useCreateRequestMutation();

  const [form, setForm] = useState<CreateRequestDto>({
    organizationId: userOrganizationId ?? "",
    bloodType: BloodType.APositive,
    unitsRequested: 1,
    urgency: RequestUrgency.Normal,
    latitude: 0,
    longitude: 0,
    notes: "",
  });

  const [errors, setErrors] =
    useState<RequestValidationErrors>({});

  const [submitError, setSubmitError] =
    useState<string | null>(null);

  const staffOrganizationId =
    currentUserLoading || currentUserError
      ? ""
      : currentUser?.organizationId ?? "";

  const assignedOrganization = organizations.find(
    (organization) =>
      organization.id === staffOrganizationId
  );

  const staffOrganizationError = isStaff
    ? currentUserLoading
      ? null
      : currentUserError || !currentUser
        ? "Unable to verify your staff account. Please sign in again."
        : !staffOrganizationId
          ? "Your staff account is not assigned to an organization. Contact an administrator."
          : organizationsError
        ? "Unable to verify your assigned organization. Please try again."
        : !organizationsLoading && !assignedOrganization
          ? "Your assigned organization could not be found. Contact an administrator."
          : null
    : null;

  const availableOrganizations = isStaff
    ? organizations.filter(
        (organization) =>
          organization.id === staffOrganizationId
      )
    : organizations;

  useEffect(() => {
    const organizationId = isStaff
      ? staffOrganizationId
      : userOrganizationId;

    if (!organizationId) {
      if (isStaff) {
        setForm((current) => ({
          ...current,
          organizationId: "",
          latitude: Number.NaN,
          longitude: Number.NaN,
        }));
      }
      return;
    }

    const selectedOrganization = organizations.find(
      (organization) =>
        organization.id === organizationId
    );

    const coordinatesAvailable = (
      selectedOrganization !== undefined &&
      Number.isFinite(selectedOrganization.latitude) &&
      Number.isFinite(selectedOrganization.longitude)
    );

    setForm((current) => ({
      ...current,
      organizationId,
      latitude: coordinatesAvailable
        ? selectedOrganization.latitude
        : Number.NaN,
      longitude: coordinatesAvailable
        ? selectedOrganization.longitude
        : Number.NaN,
    }));
  }, [
    currentUser?.organizationId,
    currentUserError,
    currentUserLoading,
    isStaff,
    organizations,
    staffOrganizationId,
    userOrganizationId,
  ]);

  const updateField = <K extends keyof CreateRequestDto>(
    field: K,
    value: CreateRequestDto[K]
  ) => {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));

    setErrors((current) => ({
      ...current,
      [field]: undefined,
    }));

    setSubmitError(null);
  };

  const handleOrganizationChange = (
    organizationId: string
  ) => {
    if (isStaff) {
      return;
    }

    const selectedOrganization = organizations.find(
      (organization) => organization.id === organizationId
    );

    const coordinatesAvailable = (
      selectedOrganization !== undefined &&
      Number.isFinite(selectedOrganization.latitude) &&
      Number.isFinite(selectedOrganization.longitude)
    );

    setForm((current) => ({
      ...current,
      organizationId,
      latitude: coordinatesAvailable
        ? selectedOrganization.latitude
        : Number.NaN,
      longitude: coordinatesAvailable
        ? selectedOrganization.longitude
        : Number.NaN,
    }));

    setErrors((current) => ({
      ...current,
      organizationId: undefined,
      latitude:
        organizationId && !coordinatesAvailable
          ? "The selected organization does not have valid coordinates."
          : undefined,
      longitude:
        organizationId && !coordinatesAvailable
          ? "The selected organization does not have valid coordinates."
          : undefined,
    }));

    setSubmitError(null);
  };

  const handleSubmit = async (
    event: FormEvent<HTMLFormElement>
  ) => {
    event.preventDefault();

    if (!userId) {
      setSubmitError(
        "Unable to identify the logged-in user. Please sign in again."
      );
      return;
    }

    if (isStaff && currentUserLoading) {
      setSubmitError(
        "Please wait while your assigned organization is verified."
      );
      return;
    }

    if (staffOrganizationError) {
      setErrors((current) => ({
        ...current,
        organizationId: staffOrganizationError,
      }));
      setSubmitError(staffOrganizationError);
      return;
    }

    const payload: CreateRequestDto = {
      ...form,
      organizationId: isStaff
        ? staffOrganizationId
        : form.organizationId,
      notes: form.notes.trim(),
    };

    const validationErrors =
      validateCreateRequest(payload);

    setErrors(validationErrors);

    if (
      hasRequestValidationErrors(validationErrors)
    ) {
      return;
    }

    try {
      const createdRequest =
        await createRequest(payload).unwrap();

      navigate(`/requests/${createdRequest.id}`);
    } catch (error: unknown) {
      setSubmitError(
        getApiErrorMessage(
          error,
          "Unable to create the blood request. Please check the information and try again."
        )
      );
    }
  };

  return (
    <main className="requests-page">
      <section className="request-form-hero">
        <div>
          <span className="requests-eyebrow">
            BLOOD REQUEST MANAGEMENT
          </span>

          <h1>Create Blood Request</h1>

          <p>
            Record a new blood requirement and begin the
            coordination process.
          </p>
        </div>

        <Link
          to="/requests"
          className="requests-secondary-button"
        >
          ← Back to Requests
        </Link>
      </section>

      <section className="request-form-layout">
        <form
          className="request-form-card"
          onSubmit={handleSubmit}
          noValidate
        >
          <div className="request-form-heading">
            <span className="requests-eyebrow">
              REQUEST DETAILS
            </span>

            <h2>Blood Requirement</h2>

            <p>
              Enter the required blood type, quantity,
              urgency and organization information.
            </p>
          </div>

          <div className="request-form-grid">
            <div className="request-field">
              <label htmlFor="organizationId">
                Organization
              </label>

              <select
                id="organizationId"
                value={isStaff
                  ? staffOrganizationId
                  : form.organizationId}
                disabled={
                  organizationsLoading ||
                  currentUserLoading ||
                  isStaff
                }
                onChange={(event) =>
                  handleOrganizationChange(
                    event.target.value
                  )
                }
              >
                {isStaff
                  ? staffOrganizationId && !assignedOrganization
                    ? (
                      <option value={staffOrganizationId}>
                        {organizationsLoading
                          ? "Loading assigned organization..."
                          : "Assigned organization unavailable"}
                      </option>
                    )
                    : !staffOrganizationId
                      ? (
                        <option value="">
                          {currentUserLoading
                            ? "Loading staff account..."
                            : "Assigned organization unavailable"}
                        </option>
                      )
                      : null
                  : (
                    <option value="">
                      {organizationsLoading
                        ? "Loading organizations..."
                        : "Select organization"}
                    </option>
                  )}

                {availableOrganizations.map((organization) => (
                  <option
                    key={organization.id}
                    value={organization.id}
                  >
                    {organization.name}
                  </option>
                ))}
              </select>

              {errors.organizationId && !staffOrganizationError && (
                <span className="request-field-error">
                  {errors.organizationId}
                </span>
              )}

              {staffOrganizationError && (
                <span className="request-field-error">
                  {staffOrganizationError}
                </span>
              )}

              {organizationsError && !isStaff && (
                <span className="request-field-error">
                  Unable to load organizations.
                </span>
              )}
            </div>

            <div className="request-field">
              <label htmlFor="bloodType">
                Blood Type
              </label>

              <select
                id="bloodType"
                value={form.bloodType}
                onChange={(event) =>
                  updateField(
                    "bloodType",
                    event.target.value as BloodType
                  )
                }
              >
                {bloodTypeOptions.map((option) => (
                  <option
                    key={option.value}
                    value={option.value}
                  >
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="request-field">
              <label htmlFor="unitsRequested">
                Units Required
              </label>

              <input
                id="unitsRequested"
                type="number"
                min={1}
                max={100}
                value={form.unitsRequested}
                onChange={(event) =>
                  updateField(
                    "unitsRequested",
                    Number(event.target.value)
                  )
                }
              />

              {errors.unitsRequested && (
                <span className="request-field-error">
                  {errors.unitsRequested}
                </span>
              )}
            </div>

            <div className="request-field">
              <label htmlFor="urgency">
                Urgency
              </label>

              <select
                id="urgency"
                value={form.urgency}
                onChange={(event) =>
                  updateField(
                    "urgency",
                    event.target.value as RequestUrgency
                  )
                }
              >
                {urgencyOptions.map((option) => (
                  <option
                    key={option.value}
                    value={option.value}
                  >
                    {option.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="request-field">
              <label>Requester</label>

              <div className="request-readonly-value">
                Logged-in user
              </div>

              <span className="request-field-help">
                Requester information is automatically
                taken from your account.
              </span>
            </div>

            <div className="request-field">
              <label htmlFor="latitude">
                Latitude
              </label>

              <input
                id="latitude"
                type="number"
                step="any"
                min={-90}
                max={90}
                value={Number.isFinite(form.latitude) ? form.latitude : ""}
                readOnly
              />

              {errors.latitude && (
                <span className="request-field-error">
                  {errors.latitude}
                </span>
              )}
            </div>

            <div className="request-field">
              <label htmlFor="longitude">
                Longitude
              </label>

              <input
                id="longitude"
                type="number"
                step="any"
                min={-180}
                max={180}
                value={Number.isFinite(form.longitude) ? form.longitude : ""}
                readOnly
              />

              {errors.longitude && (
                <span className="request-field-error">
                  {errors.longitude}
                </span>
              )}
            </div>

            <div className="request-field request-field--full">
              <label htmlFor="notes">
                Notes
              </label>

              <textarea
                id="notes"
                rows={5}
                maxLength={1000}
                placeholder="Add clinical or operational notes..."
                value={form.notes}
                onChange={(event) =>
                  updateField(
                    "notes",
                    event.target.value
                  )
                }
              />

              <div className="request-field-meta">
                <span>
                  {errors.notes ?? ""}
                </span>

                <span>
                  {form.notes.length}/1000
                </span>
              </div>
            </div>
          </div>

          {(submitError || createError) && (
            <div
              className="request-form-alert request-form-alert--error"
              role="alert"
            >
              {submitError ??
                "Unable to create the blood request."}
            </div>
          )}

          <div className="request-form-actions">
            <Link
              to="/requests"
              className="requests-secondary-button"
            >
              Cancel
            </Link>

            <button
              type="submit"
              className="requests-primary-button"
              disabled={
                isCreating ||
                organizationsLoading ||
                currentUserLoading ||
                Boolean(staffOrganizationError) ||
                !userId
              }
            >
              {isCreating
                ? "Creating Request..."
                : "Create Blood Request"}
            </button>
          </div>
        </form>

        <aside className="request-form-side-card">
          <div className="request-form-side-icon">
            ♥
          </div>

          <span className="requests-eyebrow">
            COORDINATION READY
          </span>

          <h3>Accurate details matter</h3>

          <p>
            Blood type, urgency, required units and
            organization location help the coordination
            workflow respond correctly.
          </p>

          <div className="request-form-tip">
            <strong>Critical request?</strong>
            <span>
              Select Critical urgency so staff can
              identify it immediately.
            </span>
          </div>
        </aside>
      </section>
    </main>
  );
}