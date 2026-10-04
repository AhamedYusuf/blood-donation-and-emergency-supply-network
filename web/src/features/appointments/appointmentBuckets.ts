import type { Appointment } from "./appointmentsApi";

export interface DonorAppointmentBuckets {
  pending: Appointment[];
  completed: Appointment[];
  other: Appointment[];
}

export function splitDonorAppointments(
  appointments: Appointment[],
  now = new Date(),
): DonorAppointmentBuckets {
  const pending: Appointment[] = [];
  const completed: Appointment[] = [];
  const other: Appointment[] = [];

  for (const appointment of appointments) {
    const isFuture = new Date(appointment.scheduledTime) >= now;
    if (appointment.status === "completed") {
      completed.push(appointment);
    } else if (
      isFuture &&
      (appointment.status === "scheduled" || appointment.status === "pending_confirmation")
    ) {
      pending.push(appointment);
    } else {
      other.push(appointment);
    }
  }

  pending.sort((a, b) => a.scheduledTime.localeCompare(b.scheduledTime));
  completed.sort((a, b) => b.scheduledTime.localeCompare(a.scheduledTime));
  other.sort((a, b) => b.scheduledTime.localeCompare(a.scheduledTime));
  return { pending, completed, other };
}
