namespace BloodDonationNetwork.Domain.Entities;

public enum AppointmentStatus
{
    Scheduled,
    Completed,
    NoShow,
    Cancelled,

    // Appended, not inserted — Status is a plain `integer` column with no
    // check constraint, so adding members here is safe, but reordering or
    // inserting between existing ones would silently relabel every
    // already-stored appointment's status.

    // Set only when an appointment is created by the Matching & Dispatch
    // Agent's own dispatch (CreateAppointmentDto.RelatedWorkflowId set) —
    // the donor didn't request this slot themselves, so it waits for an
    // explicit accept/decline before counting as a real commitment. A
    // donor-initiated booking (no related workflow) still goes straight
    // to Scheduled, same as before — booking it themselves already is
    // the confirmation.
    PendingConfirmation,

    // The donor explicitly declined a PendingConfirmation appointment —
    // distinct from Cancelled (which means they'd accepted, then backed
    // out) so the two read differently in history and reporting.
    Declined
}