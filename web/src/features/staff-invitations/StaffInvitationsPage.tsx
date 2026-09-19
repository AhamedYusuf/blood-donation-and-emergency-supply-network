import { useState, type FormEvent } from "react";
import { FormField } from "../../components/FormField";
import { useGetOrganizationsQuery } from "../organizations/organizationsApi";
import { useCreateStaffInvitationMutation, useGetStaffInvitationsQuery } from "./staffInvitationsApi";
import type { StaffInvitation } from "./staffInvitationTypes";

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function statusFor(invitation: StaffInvitation) {
  if (invitation.acceptedAt) return { label: "Accepted", color: "#166534", background: "#DCFCE7" };
  if (new Date(invitation.expiresAt).getTime() <= Date.now()) return { label: "Expired", color: "#991B1B", background: "#FEE2E2" };
  return { label: "Pending", color: "#92400E", background: "#FEF3C7" };
}

function formatDate(value: string | null) {
  if (!value) return "—";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : date.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

function apiMessage(error: unknown) {
  const value = error as { data?: { message?: string } } | undefined;
  return value?.data?.message ?? "Could not create the invitation. Please try again.";
}

export function StaffInvitationsPage() {
  const { data: organizations = [], isLoading: loadingOrganizations } = useGetOrganizationsQuery();
  const [organizationId, setOrganizationId] = useState("");
  const [email, setEmail] = useState("");
  const [touched, setTouched] = useState({ email: false, organization: false });
  const [apiError, setApiError] = useState<string | null>(null);
  const [createdInvitation, setCreatedInvitation] = useState<StaffInvitation | null>(null);
  const [copied, setCopied] = useState(false);
  const [createInvitation, createState] = useCreateStaffInvitationMutation();
  const { data: invitations = [], isLoading: loadingInvitations } = useGetStaffInvitationsQuery(organizationId, { skip: !organizationId });

  const emailError = touched.email && !email.trim() ? "Email is required." : touched.email && !EMAIL_RE.test(email.trim()) ? "Enter a valid email address." : "";
  const organizationError = touched.organization && !organizationId ? "Select an organization." : "";
  const valid = EMAIL_RE.test(email.trim()) && Boolean(organizationId);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setTouched({ email: true, organization: true });
    setApiError(null);
    setCopied(false);
    if (!valid) return;
    try {
      const invitation = await createInvitation({ email: email.trim(), organizationId }).unwrap();
      setCreatedInvitation(invitation);
      setEmail("");
      setTouched({ email: false, organization: false });
    } catch (error) {
      setApiError(apiMessage(error));
    }
  };

  const copyToken = async () => {
    if (!createdInvitation) return;
    await navigator.clipboard.writeText(createdInvitation.token);
    setCopied(true);
  };

  return (
    <main style={{ height: "100%", overflow: "auto", padding: "var(--space-xl)", background: "var(--color-canvas)" }}>
      <div style={{ maxWidth: 1100, margin: "0 auto" }}>
        <div style={{ marginBottom: "var(--space-lg)" }}>
          <p className="text-caption" style={{ color: "var(--color-primary)", letterSpacing: "0.08em", fontWeight: 700 }}>ADMINISTRATION</p>
          <h1 className="text-heading" style={{ margin: "4px 0", color: "var(--color-ink)" }}>Invite staff</h1>
          <p className="text-body-sm" style={{ color: "var(--color-ink-muted)", margin: 0 }}>Create a time-limited invitation for a member of your organization.</p>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "minmax(280px, 380px) 1fr", gap: "var(--space-lg)", alignItems: "start" }}>
          <section style={{ background: "var(--color-surface)", border: "1px solid var(--color-hairline)", borderRadius: "var(--radius-md)", padding: "var(--space-lg)" }}>
            <h2 className="text-subheading" style={{ margin: "0 0 4px" }}>New invitation</h2>
            <p className="text-caption" style={{ color: "var(--color-ink-muted)", margin: "0 0 var(--space-md)" }}>The invitee will use the token to create their staff account.</p>
            <form onSubmit={submit} noValidate style={{ display: "flex", flexDirection: "column", gap: "var(--space-md)" }}>
              <FormField id="staff-invite-email" label="Invitee email" type="email" required value={email} placeholder="staff@example.com" error={emailError} onChange={(event) => setEmail(event.target.value)} onBlur={() => setTouched((current) => ({ ...current, email: true }))} />
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                <label htmlFor="staff-invite-organization" className="text-label" style={{ color: "var(--color-ink-secondary)" }}>Organization <span style={{ color: "var(--color-critical)" }}>*</span></label>
                <select id="staff-invite-organization" required value={organizationId} disabled={loadingOrganizations} onChange={(event) => setOrganizationId(event.target.value)} onBlur={() => setTouched((current) => ({ ...current, organization: true }))} style={{ padding: "8px 12px", border: organizationError ? "1px solid var(--color-critical)" : "1px solid var(--color-hairline-strong)", borderRadius: "var(--radius-sm)", background: "var(--color-surface)", color: "var(--color-ink)", font: "inherit" }}>
                  <option value="">{loadingOrganizations ? "Loading organizations…" : "Select an organization…"}</option>
                  {organizations.map((organization) => <option key={organization.id} value={organization.id}>{organization.name}</option>)}
                </select>
                {organizationError && <span className="text-caption" style={{ color: "var(--color-critical)" }}>{organizationError}</span>}
              </div>
              {apiError && <div role="alert" className="text-body-sm" style={{ color: "var(--color-critical)", background: "var(--color-critical-subtle)", padding: "10px 12px", borderRadius: "var(--radius-sm)" }}>{apiError}</div>}
              <button type="submit" disabled={createState.isLoading || loadingOrganizations} style={{ border: 0, borderRadius: "var(--radius-sm)", padding: "10px 14px", background: "var(--color-primary)", color: "var(--color-on-primary)", font: "inherit", fontWeight: 700, cursor: createState.isLoading ? "wait" : "pointer" }}>{createState.isLoading ? "Creating…" : "Create invitation"}</button>
            </form>
          </section>
          <div>
            {createdInvitation && <section role="status" style={{ background: "var(--color-surface)", border: "1px solid var(--color-primary)", borderRadius: "var(--radius-md)", padding: "var(--space-lg)", marginBottom: "var(--space-lg)" }}>
              <p className="text-caption" style={{ color: "var(--color-primary)", fontWeight: 700, letterSpacing: "0.06em", margin: 0 }}>COPY THIS TOKEN NOW</p>
              <h2 className="text-subheading" style={{ margin: "4px 0" }}>Invitation created</h2>
              <p className="text-body-sm" style={{ color: "var(--color-ink-muted)", margin: "0 0 var(--space-sm)" }}>This token is shown only once. Share it securely with the invitee.</p>
              <div style={{ display: "flex", gap: 8, alignItems: "stretch" }}><code style={{ flex: 1, overflowWrap: "anywhere", padding: "10px 12px", background: "var(--color-surface-sunken)", borderRadius: "var(--radius-sm)", color: "var(--color-ink)" }}>{createdInvitation.token}</code><button type="button" onClick={copyToken} style={{ border: "1px solid var(--color-hairline-strong)", borderRadius: "var(--radius-sm)", background: "var(--color-surface)", color: "var(--color-ink)", padding: "0 12px", cursor: "pointer", font: "inherit" }}>{copied ? "Copied" : "Copy"}</button></div>
            </section>}
            <section style={{ background: "var(--color-surface)", border: "1px solid var(--color-hairline)", borderRadius: "var(--radius-md)", padding: "var(--space-lg)" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline", marginBottom: "var(--space-md)" }}><div><h2 className="text-subheading" style={{ margin: 0 }}>Invitation history</h2><p className="text-caption" style={{ color: "var(--color-ink-muted)", margin: "4px 0 0" }}>Select an organization to view its invitations.</p></div><span className="text-caption" style={{ color: "var(--color-ink-muted)" }}>{invitations.length} total</span></div>
              {!organizationId ? <p className="text-body-sm" style={{ color: "var(--color-ink-muted)" }}>No organization selected.</p> : loadingInvitations ? <p className="text-body-sm">Loading invitations…</p> : invitations.length === 0 ? <p className="text-body-sm" style={{ color: "var(--color-ink-muted)" }}>No invitations for this organization yet.</p> : <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>{invitations.map((invitation) => { const status = statusFor(invitation); return <div key={invitation.id} style={{ display: "grid", gridTemplateColumns: "minmax(150px, 1fr) auto auto", gap: 12, alignItems: "center", padding: "10px 0", borderBottom: "1px solid var(--color-hairline)" }}><div><div className="text-body-sm" style={{ fontWeight: 600 }}>{invitation.email}</div><div className="text-caption" style={{ color: "var(--color-ink-muted)" }}>Created {formatDate(invitation.createdAt)} · Expires {formatDate(invitation.expiresAt)}</div></div><span className="text-caption" style={{ color: status.color, background: status.background, borderRadius: 999, padding: "3px 8px", fontWeight: 700 }}>{status.label}</span><span className="text-caption" style={{ color: "var(--color-ink-muted)" }}>{formatDate(invitation.acceptedAt)}</span></div>; })}</div>}
            </section>
          </div>
        </div>
      </div>
    </main>
  );
}
