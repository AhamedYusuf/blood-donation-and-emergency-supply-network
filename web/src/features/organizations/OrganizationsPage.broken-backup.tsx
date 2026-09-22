import { useMemo, useState } from "react";
import { BloodDrops } from "../../components/blood-effects/BloodDrops";
import {
  useCreateOrganizationMutation,
  useDeleteOrganizationMutation,
  useGetOrganizationsQuery,
  useUpdateOrganizationMutation,
} from "./organizationsApi";
import type {
  Organization,
  OrganizationFormData,
} from "./organizationTypes";
import "./organizations.css";
import {
  hasOrganizationErrors,
  validateOrganizationForm,
  type OrganizationFieldErrors,
} from "./organizationValidation";
const EMPTY_FORM: OrganizationFormData = {
  name: "",
  type: "BloodBank",
  address: "",
  latitude: Number.NaN,
  longitude: Number.NaN,
  phoneNumber: "",
};

const TYPE_OPTIONS = [
  {
    value: "BloodBank",
    label: "Blood Bank",
    description: "Blood collection and storage",
  },
  {
    value: "Hospital",
    label: "Hospital",
    description: "Hospital or medical center",
  },
  {
    value: "EmergencyCenter",
    label: "Emergency Center",
    description: "Emergency response facility",
  },
];

function formatType(type: string) {
  const normalized = type.toLowerCase();

  if (normalized.includes("blood")) return "Blood Bank";
  if (normalized.includes("hospital")) return "Hospital";
  if (normalized.includes("emergency")) return "Emergency Center";

  return type;
}

function getTypeIcon(type: string) {
  const normalized = type.toLowerCase();

  if (normalized.includes("hospital")) {
    return "✚";
  }

  if (normalized.includes("emergency")) {
    return "⚠";
  }

  return "♥";
}

function getTypeClass(type: string) {
  const normalized = type.toLowerCase();

  if (normalized.includes("hospital")) {
    return "organization-card__icon--hospital";
  }

  if (normalized.includes("emergency")) {
    return "organization-card__icon--emergency";
  }

  return "organization-card__icon--blood";
}

function formatDate(value: string) {
  if (!value) return "—";

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function OrganizationModal({
  mode,
  form,
  setForm,
  errors,
  isSaving,
  onClose,
  onSubmit,
}: {
  mode: "create" | "edit";
  form: OrganizationFormData;
  setForm: React.Dispatch<React.SetStateAction<OrganizationFormData>>;
  errors: OrganizationFieldErrors;
  isSaving: boolean;
  onClose: () => void;
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void;
}) {
  const updateField = (
    field: keyof OrganizationFormData,
    value: string | number,
  ) => {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));
  };

  return (
    <div
      className="organization-modal-backdrop"
      role="presentation"
      onMouseDown={onClose}
    >
      <div
        className="organization-modal"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="organization-modal__header">
          <div>
            <span className="organization-modal__eyebrow">
              {mode === "create" ? "NEW PARTNER" : "UPDATE PARTNER"}
            </span>
            <h2>
              {mode === "create"
                ? "Add an organization"
                : "Edit organization"}
            </h2>
            <p>
              Keep your blood donation network accurate and connected.
            </p>
          </div>

          <button
            type="button"
            className="organization-modal__close"
            onClick={onClose}
            aria-label="Close modal"
          >
            ×
          </button>
        </div>

        <form onSubmit={onSubmit} noValidate>
          <div className="organization-form">
            <label className="organization-field organization-field--full">
              <span>Organization name</span>
              <input
                required
                minLength={2}
                maxLength={200}
                className={errors.name ? "has-error" : ""}
                value={form.name}
                onChange={(event) =>
                  updateField("name", event.target.value)
                }
                placeholder="e.g. Colombo Central Blood Bank"
                aria-invalid={Boolean(errors.name)}
              />
              {errors.name && (
                <small className="organization-field__error">
                  {errors.name}
                </small>
              )}
            </label>

            <label className="organization-field">
              <span>Organization type</span>
              <select
                required
                className={errors.type ? "has-error" : ""}
                value={form.type}
                onChange={(event) =>
                  updateField("type", event.target.value)
                }
                aria-invalid={Boolean(errors.type)}
              >
                {TYPE_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              {errors.type && (
                <small className="organization-field__error">
                  {errors.type}
                </small>
              )}
            </label>

            <label className="organization-field">
              <span>Phone number</span>
              <input
                required
                type="tel"
                inputMode="numeric"
                maxLength={10}
                className={errors.phoneNumber ? "has-error" : ""}
                value={form.phoneNumber}
                onChange={(event) =>
                  updateField(
                    "phoneNumber",
                    event.target.value.replace(/\D/g, "").slice(0, 10),
                  )
                }
                placeholder="0112691111"
                aria-invalid={Boolean(errors.phoneNumber)}
              />
              {errors.phoneNumber && (
                <small className="organization-field__error">
                  {errors.phoneNumber}
                </small>
              )}
            </label>

            <label className="organization-field organization-field--full">
              <span>Address</span>
              <textarea
                required
                minLength={5}
                maxLength={500}
                rows={3}
                className={errors.address ? "has-error" : ""}
                value={form.address}
                onChange={(event) =>
                  updateField("address", event.target.value)
                }
                placeholder="Enter the complete organization address"
                aria-invalid={Boolean(errors.address)}
              />
              {errors.address && (
                <small className="organization-field__error">
                  {errors.address}
                </small>
              )}
            </label>

            <div className="organization-location-heading">
              <div>
                <strong>Location coordinates</strong>
                <span>
                  Used for nearby organizations and emergency routing.
                </span>
              </div>
            </div>

            <label className="organization-field">
              <span>Latitude</span>
              <input
                required
                type="number"
                min="-90"
                max="90"
                step="any"
                className={errors.latitude ? "has-error" : ""}
                value={Number.isNaN(form.latitude) ? "" : form.latitude}
                onChange={(event) =>
                  updateField(
                    "latitude",
                    event.target.value === ""
                      ? Number.NaN
                      : Number(event.target.value),
                  )
                }
                aria-invalid={Boolean(errors.latitude)}
              />
              {errors.latitude && (
                <small className="organization-field__error">
                  {errors.latitude}
                </small>
              )}
            </label>

            <label className="organization-field">
              <span>Longitude</span>
              <input
                required
                type="number"
                min="-180"
                max="180"
                step="any"
                className={errors.longitude ? "has-error" : ""}
                value={Number.isNaN(form.longitude) ? "" : form.longitude}
                onChange={(event) =>
                  updateField(
                    "longitude",
                    event.target.value === ""
                      ? Number.NaN
                      : Number(event.target.value),
                  )
                }
                aria-invalid={Boolean(errors.longitude)}
              />
              {errors.longitude && (
                <small className="organization-field__error">
                  {errors.longitude}
                </small>
              )}
            </label>
          </div>

          <div className="organization-modal__footer">
            <button
              type="button"
              className="organization-button organization-button--secondary"
              onClick={onClose}
              disabled={isSaving}
            >
              Cancel
            </button>

            <button
              type="submit"
              className="organization-button organization-button--primary"
              disabled={isSaving}
            >
              {isSaving
                ? "Saving..."
                : mode === "create"
                  ? "Create Organization"
                  : "Save Changes"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function DeleteModal({
  organization,
  isDeleting,
  onCancel,
  onConfirm,
}: {
  organization: Organization;
  isDeleting: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  return (
    <div
      className="organization-modal-backdrop"
      onMouseDown={onCancel}
    >
      <div
        className="organization-delete-modal"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="delete-modal__icon">!</div>

        <span className="organization-modal__eyebrow">
          REMOVE ORGANIZATION
        </span>

        <h2>Are you sure?</h2>

        <p>
          You are about to remove{" "}
          <strong>{organization.name}</strong> from the network.
          This action cannot be undone.
        </p>

        <div className="organization-modal__footer">
          <button
            type="button"
            className="organization-button organization-button--secondary"
            onClick={onCancel}
            disabled={isDeleting}
          >
            Cancel
          </button>

          <button
            type="button"
            className="organization-button organization-button--danger"
            onClick={onConfirm}
            disabled={isDeleting}
          >
            {isDeleting ? "Deleting..." : "Delete Organization"}
          </button>
        </div>
      </div>
    </div>
  );
}

export function OrganizationsPage() {
  const {
    data: organizations = [],
    isLoading,
    isFetching,
    error,
  } = useGetOrganizationsQuery();

  const [createOrganization, createState] =
    useCreateOrganizationMutation();

  const [updateOrganization, updateState] =
    useUpdateOrganizationMutation();

  const [deleteOrganization, deleteState] =
    useDeleteOrganizationMutation();

  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("All");

  const [modalMode, setModalMode] =
    useState<"create" | "edit" | null>(null);

  const [selectedOrganization, setSelectedOrganization] =
    useState<Organization | null>(null);

  const [deleteTarget, setDeleteTarget] =
    useState<Organization | null>(null);

  const [form, setForm] =
    useState<OrganizationFormData>(EMPTY_FORM);

  const [actionError, setActionError] = useState("");
  const [formErrors, setFormErrors] = useState<OrganizationFieldErrors>({});

  const filteredOrganizations = useMemo(() => {
    const query = search.trim().toLowerCase();

    return organizations.filter((organization) => {
      const matchesSearch =
        !query ||
        organization.name.toLowerCase().includes(query) ||
        organization.address.toLowerCase().includes(query) ||
        organization.type.toLowerCase().includes(query) ||
        organization.phoneNumber.toLowerCase().includes(query);

      const matchesType =
        typeFilter === "All" ||
        formatType(organization.type) === typeFilter;

      return matchesSearch && matchesType;
    });
  }, [organizations, search, typeFilter]);

  const bloodBanks = organizations.filter((organization) =>
    organization.type.toLowerCase().includes("blood"),
  ).length;

  const hospitals = organizations.filter((organization) =>
    organization.type.toLowerCase().includes("hospital"),
  ).length;

  const emergencyCenters = organizations.filter((organization) =>
    organization.type.toLowerCase().includes("emergency"),
  ).length;

  const openCreateModal = () => {
    setActionError("");
    setFormErrors({});
    setForm(EMPTY_FORM);
    setSelectedOrganization(null);
    setModalMode("create");
  };

  const openEditModal = (organization: Organization) => {
    setActionError("");
    setFormErrors({});
    setSelectedOrganization(organization);

    setForm({
      name: organization.name,
      type: organization.type,
      address: organization.address,
      latitude: organization.latitude,
      longitude: organization.longitude,
      phoneNumber: organization.phoneNumber,
    });

    setModalMode("edit");
  };

  const closeModal = () => {
    if (createState.isLoading || updateState.isLoading) {
      return;
    }

    setModalMode(null);
    setSelectedOrganization(null);
    setActionError("");
    setFormErrors({});
  };

  const handleSubmit = async (
    event: React.FormEvent<HTMLFormElement>,
  ) => {
    event.preventDefault();
    setActionError("");

    const errors = validateOrganizationForm(form);
    setFormErrors(errors);

    if (hasOrganizationErrors(errors)) {
      return;
    }

    try {
      if (modalMode === "create") {
        await createOrganization(form).unwrap();
      } else if (
        modalMode === "edit" &&
        selectedOrganization
      ) {
        await updateOrganization({
          id: selectedOrganization.id,
          body: form,
        }).unwrap();
      }

      closeModal();
    } catch (requestError) {
      console.error(requestError);
      setActionError(
        "We could not save this organization. Please check the information and try again.",
      );
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;

    setActionError("");

    try {
      await deleteOrganization(deleteTarget.id).unwrap();
      setDeleteTarget(null);
    } catch (requestError) {
      console.error(requestError);
      setActionError(
        "This organization could not be deleted. It may still be connected to users or inventory.",
      );
    }
  };

  return (
    <main className="organizations-page">
      <section className="organizations-hero">
        <BloodDrops count={10} intensity="normal" />

        <div className="organizations-hero__glow" />
        <div className="organizations-hero__content">
          <div className="organizations-hero__copy">
            <span className="organizations-eyebrow">
              BLOOD DONATION NETWORK
            </span>

            <h1>
              Every organization.
              <br />
              <span>One life-saving network.</span>
            </h1>

            <p>
              Connect blood banks, hospitals and emergency
              partners to keep critical blood supplies moving
              where they are needed most.
            </p>

            <div className="organizations-hero__actions">
              <button
                type="button"
                className="organization-button organization-button--primary organization-button--hero"
                onClick={openCreateModal}
              >
                <span>+</span>
                Add Organization
              </button>

              <div className="organizations-hero__trust">
                <span className="trust-dot" />
                Live network data
              </div>
            </div>
          </div>

          <div className="organizations-hero__visual">
            <div className="hero-blood-orbit hero-blood-orbit--one" />
            <div className="hero-blood-orbit hero-blood-orbit--two" />

            <div className="hero-blood-drop">
              <div className="hero-blood-drop__highlight" />
            </div>

            <div className="hero-pulse-line">
              <span />
              <span />
              <span />
              <span />
              <span />
            </div>

            <div className="hero-network-card">
              <span>NETWORK STATUS</span>
              <strong>Connected</strong>
              <small>
                {organizations.length} active partners
              </small>
            </div>
          </div>
        </div>

        <div className="organizations-hero__wave">
          <div />
          <div />
        </div>
      </section>

      <section className="organizations-content">
        <div className="organization-stat-grid">
          <div className="organization-stat-card">
            <div className="organization-stat-card__icon">
              ♥
            </div>
            <div>
              <span>Total organizations</span>
              <strong>{organizations.length}</strong>
              <small>Across the network</small>
            </div>
          </div>

          <div className="organization-stat-card">
            <div className="organization-stat-card__icon">
              ♥
            </div>
            <div>
              <span>Blood banks</span>
              <strong>{bloodBanks}</strong>
              <small>Supply partners</small>
            </div>
          </div>

          <div className="organization-stat-card">
            <div className="organization-stat-card__icon">
              ✚
            </div>
            <div>
              <span>Hospitals</span>
              <strong>{hospitals}</strong>
              <small>Care partners</small>
            </div>
          </div>

          <div className="organization-stat-card">
            <div className="organization-stat-card__icon">
              ⚠
            </div>
            <div>
              <span>Emergency centers</span>
              <strong>{emergencyCenters}</strong>
              <small>Rapid response</small>
            </div>
          </div>
        </div>

        {actionError && (
          <div className="organization-alert organization-alert--error">
            <span>!</span>
            {actionError}
          </div>
        )}

        <section className="organization-list-section">
          <div className="organization-list-header">
            <div>
              <span className="organizations-eyebrow">
                PARTNER NETWORK
              </span>
              <h2>Organizations</h2>
              <p>
                Hospitals and blood banks connected to your
                network.
              </p>
            </div>

            <button
              type="button"
              className="organization-button organization-button--primary"
              onClick={openCreateModal}
            >
              <span>+</span>
              Add organization
            </button>
          </div>

          <div className="organization-toolbar">
            <div className="organization-search">
              <span>⌕</span>
              <input
                value={search}
                onChange={(event) =>
                  setSearch(event.target.value)
                }
                placeholder="Search organizations..."
              />
              {search && (
                <button
                  type="button"
                  onClick={() => setSearch("")}
                  aria-label="Clear search"
                >
                  ×
                </button>
              )}
            </div>

            <div className="organization-filters">
              {["All", "Blood Bank", "Hospital", "Emergency Center"].map(
                (filter) => (
                  <button
                    key={filter}
                    type="button"
                    className={
                      typeFilter === filter
                        ? "active"
                        : ""
                    }
                    onClick={() => setTypeFilter(filter)}
                  >
                    {filter}
                  </button>
                ),
              )}
            </div>
          </div>

          {isLoading ? (
            <div className="organization-grid">
              {Array.from({ length: 6 }).map((_, index) => (
                <div
                  className="organization-card organization-card--skeleton"
                  key={index}
                >
                  <div className="skeleton skeleton--icon" />
                  <div className="skeleton skeleton--title" />
                  <div className="skeleton skeleton--line" />
                  <div className="skeleton skeleton--line skeleton--short" />
                  <div className="skeleton skeleton--footer" />
                </div>
              ))}
            </div>
          ) : error ? (
            <div className="organization-empty">
              <div className="organization-empty__icon">!</div>
              <h3>Unable to load organizations</h3>
              <p>
                Please make sure you are signed in and the API
                server is running.
              </p>
            </div>
          ) : filteredOrganizations.length === 0 ? (
            <div className="organization-empty">
              <div className="organization-empty__icon">♥</div>
              <h3>
                {organizations.length === 0
                  ? "No organizations yet"
                  : "No matching organizations"}
              </h3>
              <p>
                {organizations.length === 0
                  ? "Add your first blood bank or healthcare partner to start building the network."
                  : "Try another search term or change the organization filter."}
              </p>

              {organizations.length === 0 && (
                <button
                  type="button"
                  className="organization-button organization-button--primary"
                  onClick={openCreateModal}
                >
                  Add first organization
                </button>
              )}
            </div>
          ) : (
            <div className="organization-grid">
              {filteredOrganizations.map((organization) => (
                <article
                  className="organization-card"
                  key={organization.id}
                >
                  <div className="organization-card__top">
                    <div
                      className={`organization-card__icon ${getTypeClass(
                        organization.type,
                      )}`}
                    >
                      {getTypeIcon(organization.type)}
                    </div>

                    <span className="organization-status">
                      <i />
                      Connected
                    </span>
                  </div>

                  <div className="organization-card__body">
                    <span className="organization-card__type">
                      {formatType(organization.type)}
                    </span>

                    <h3>{organization.name}</h3>

                    <div className="organization-card__detail">
                      <span>⌖</span>
                      <span>{organization.address}</span>
                    </div>

                    <div className="organization-card__detail">
                      <span>☎</span>
                      <span>{organization.phoneNumber}</span>
                    </div>
                  </div>

                  <div className="organization-card__bottom">
                    <span>
                      Joined {formatDate(organization.createdAt)}
                    </span>

                    <div className="organization-card__actions">
                      <button
                        type="button"
                        onClick={() =>
                          openEditModal(organization)
                        }
                        title="Edit organization"
                      >
                        Edit
                      </button>

                      <button
                        type="button"
                        className="danger"
                        onClick={() =>
                          setDeleteTarget(organization)
                        }
                        title="Delete organization"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}

          {isFetching && !isLoading && (
            <div className="organization-refreshing">
              Updating network data...
            </div>
          )}
        </section>
      </section>

      <section className="organizations-bottom-banner">
        <BloodDrops count={6} intensity="subtle" />

        <div>
          <span className="organizations-eyebrow">
            EVERY DROP COUNTS
          </span>
          <h2>
            Stronger connections.
            <br />
            Faster help. More lives saved.
          </h2>
          <p>
            Your organization network helps critical blood
            resources reach patients when every second matters.
          </p>
        </div>

        <div className="bottom-banner-drop">
          <div />
        </div>
      </section>

      {modalMode && (
        <OrganizationModal
          mode={modalMode}
          form={form}
          setForm={setForm}
          errors={formErrors}
          isSaving={
            createState.isLoading || updateState.isLoading
          }
          onClose={closeModal}
          onSubmit={handleSubmit}
        />
      )}
 
      {deleteTarget && (
        <DeleteModal
          organization={deleteTarget}
          isDeleting={deleteState.isLoading}
          onCancel={() => setDeleteTarget(null)}
          onConfirm={handleDelete}
        />
      )}
    </main>
     );
}
