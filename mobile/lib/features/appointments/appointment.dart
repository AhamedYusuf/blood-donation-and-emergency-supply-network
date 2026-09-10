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
  final String status; // scheduled | completed | no_show | cancelled
  final String? donorBloodType;
  final int? unitsDonated;

  bool get isAgentMatched => relatedWorkflowId != null;

  /// "Active" = still on the books and in the future.
  bool get isUpcoming =>
      status == 'scheduled' && scheduledTime.isAfter(DateTime.now());

  bool get canCancel => status == 'scheduled' && scheduledTime.isAfter(DateTime.now());

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
