import { describe, expect, it } from "vitest";
import { splitDonorAppointments } from "./appointmentBuckets";
import type { Appointment } from "./appointmentsApi";

const appointment = (id: string, status: string, scheduledTime: string): Appointment => ({
  id,
  donorId: "donor",
  organizationId: "organization",
  relatedWorkflowId: null,
  scheduledTime,
  status,
  donorBloodType: "O+",
  agentMatch: null,
  unitsDonated: status === "completed" ? 1 : null,
  createdAt: scheduledTime,
  updatedAt: scheduledTime,
});

describe("splitDonorAppointments", () => {
  it("separates pending, completed, and non-active outcomes", () => {
    const now = new Date("2026-10-04T12:00:00Z");
    const result = splitDonorAppointments([
      appointment("scheduled", "scheduled", "2026-10-05T12:00:00Z"),
      appointment("agent", "pending_confirmation", "2026-10-06T12:00:00Z"),
      appointment("completed", "completed", "2026-09-01T12:00:00Z"),
      appointment("cancelled", "cancelled", "2026-09-02T12:00:00Z"),
      appointment("expired", "scheduled", "2026-09-03T12:00:00Z"),
    ], now);

    expect(result.pending.map((item) => item.id)).toEqual(["scheduled", "agent"]);
    expect(result.completed.map((item) => item.id)).toEqual(["completed"]);
    expect(result.other.map((item) => item.id)).toEqual(["expired", "cancelled"]);
  });
});
