/// Mirrors the backend AppointmentResponseDto.
class Appointment {
  Appointment({
    required this.id,
    required this.donorId,
    required this.organizationId,
    required this.scheduledTime,
    required this.status,
    this.relatedWorkflowId,
    this.donorBloodType,
    this.unitsDonated,
  });

  final String id;
  final String donorId;
  final String organizationId;
  final String? relatedWorkflowId;
  final DateTime scheduledTime;

  // scheduled | pending_confirmation | completed | no_show | cancelled | declined
  //
  // An appointment the Matching & Dispatch Agent books on the donor's
  // behalf starts as pending_confirmation rather than scheduled — the
  // donor didn't request this slot themselves, so it waits for an
  // explicit confirm/decline. A self-booked appointment still goes
  // straight to scheduled, same as before.
  final String status;
  final String? donorBloodType;
  final int? unitsDonated;

  bool get isAgentMatched => relatedWorkflowId != null;

  bool get isPendingConfirmation => status == 'pending_confirmation';

  bool get _isFuture => scheduledTime.isAfter(DateTime.now());

  /// "Active" = still on the books and in the future — pending,
  /// unconfirmed agent-dispatched appointments belong here too, since
  /// they need the donor's attention, not just confirmed ones.
  bool get isUpcoming =>
      (status == 'scheduled' || status == 'pending_confirmation') && _isFuture;

  bool get canCancel => status == 'scheduled' && _isFuture;

  bool get canConfirmOrDecline => isPendingConfirmation && _isFuture;

  bool get canReschedule =>
      (status == 'scheduled' || status == 'pending_confirmation') && _isFuture;

  factory Appointment.fromJson(Map<String, dynamic> json) => Appointment(
        id: json['id'] as String,
        donorId: json['donorId'] as String,
        organizationId: json['organizationId'] as String,
        relatedWorkflowId: json['relatedWorkflowId'] as String?,
        scheduledTime: DateTime.parse(json['scheduledTime'] as String).toLocal(),
        status: json['status'] as String,
        donorBloodType: json['donorBloodType'] as String?,
        unitsDonated: (json['unitsDonated'] as num?)?.toInt(),
      );
}

/// Minimal blood-bank shape for the booking picker. The full organizations
/// feature is Student 3's — this is a read-only helper.
class BloodBank {
  BloodBank({required this.id, required this.name, required this.address});

  final String id;
  final String name;
  final String address;

  factory BloodBank.fromJson(Map<String, dynamic> json) => BloodBank(
        id: json['id'] as String,
        name: json['name'] as String,
        address: json['address'] as String? ?? '',
      );
}
