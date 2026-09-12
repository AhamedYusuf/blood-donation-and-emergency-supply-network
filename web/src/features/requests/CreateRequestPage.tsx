import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useSelector } from "react-redux";

import type { RootState } from "../../app/store";

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
    requesterId: userId ?? "",
    organizationId: userOrganizationId ?? "",
    bloodType: BloodType.APositive,
    unitsRequested: 1,
    urgency: RequestUrgency.Normal,
    hospitalName: "",
    latitude: 0,
    longitude: 0,
    notes: "",
  });

  const [errors, setErrors] =
    useState<RequestValidationErrors>({});

  const [submitError, setSubmitError] =
    useState<string | null>(null);

  useEffect(() => {
    setForm((current) => ({
      ...current,
      requesterId: userId ?? current.requesterId,
      organizationId:
        userOrganizationId ?? current.organizationId,
    }));
  }, [userId, userOrganizationId]);

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

    const payload: CreateRequestDto = {
      ...form,
      requesterId: userId,
      hospitalName: form.hospitalName.trim(),
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
    } catch {
      setSubmitError(
        "Unable to create the blood request. Please check the information and try again."
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
              urgency and hospital information.
            </p>
          </div>

          <div className="request-form-grid">
            <div className="request-field">
              <label htmlFor="organizationId">
                Organization
              </label>

              <select
                id="organizationId"
                value={form.organizationId}
                disabled={organizationsLoading}
                onChange={(event) =>
                  updateField(
                    "organizationId",
                    event.target.value
                  )
                }
              >
                <option value="">
                  {organizationsLoading
                    ? "Loading organizations..."
                    : "Select organization"}
                </option>

                {organizations.map((organization) => (
                  <option
                    key={organization.id}
                    value={organization.id}
                  >
                    {organization.name}
                  </option>
                ))}
              </select>

              {errors.organizationId && (
                <span className="request-field-error">
                  {errors.organizationId}
                </span>
              )}

              {organizationsError && (
                <span className="request-field-error">
                  Unable to load organizations.
                </span>
              )}
            </div>

            <div className="request-field">
              <label htmlFor="hospitalName">
                Hospital Name
              </label>

              <input
                id="hospitalName"
                type="text"
                maxLength={200}
                placeholder="Enter hospital name"
                value={form.hospitalName}
                onChange={(event) =>
                  updateField(
                    "hospitalName",
                    event.target.value
                  )
                }
              />

              {errors.hospitalName && (
                <span className="request-field-error">
                  {errors.hospitalName}
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
                    Number(event.target.value) as BloodType
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
                    Number(
                      event.target.value
                    ) as RequestUrgency
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
                value={form.latitude}
                onChange={(event) =>
                  updateField(
                    "latitude",
                    Number(event.target.value)
                  )
                }
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
                value={form.longitude}
                onChange={(event) =>
                  updateField(
                    "longitude",
                    Number(event.target.value)
                  )
                }
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
            hospital location help the coordination
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