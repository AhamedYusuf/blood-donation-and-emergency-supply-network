/// Mirrors the backend RequestResponseDto.
class BloodRequest {
  const BloodRequest({
    required this.id,
    required this.requesterId,
    required this.organizationId,
    required this.bloodType,
    required this.unitsRequested,
    required this.urgency,
    required this.status,
    required this.hospitalName,
    required this.latitude,
    required this.longitude,
    required this.notes,
    required this.createdAt,
    this.fulfilledAt,
    this.closedAt,
  });

  final String id;
  final String requesterId;
  final String organizationId;
  final String bloodType;
  final int unitsRequested;
  final String urgency;
  final String status;
  final String hospitalName;
  final double latitude;
  final double longitude;
  final String notes;
  final DateTime createdAt;
  final DateTime? fulfilledAt;
  final DateTime? closedAt;

  factory BloodRequest.fromJson(Map<String, dynamic> json) => BloodRequest(
        id: json['id'] as String,
        requesterId: json['requesterId'] as String,
        organizationId: json['organizationId'] as String,
        bloodType: json['bloodType'] as String,
        unitsRequested: (json['unitsRequested'] as num).toInt(),
        urgency: json['urgency'] as String,
        status: json['status'] as String,
        hospitalName: json['hospitalName'] as String,
        latitude: (json['latitude'] as num).toDouble(),
        longitude: (json['longitude'] as num).toDouble(),
        notes: json['notes'] as String,
        createdAt: DateTime.parse(json['createdAt'] as String),
        fulfilledAt: _parseDateTime(json['fulfilledAt']),
        closedAt: _parseDateTime(json['closedAt']),
      );

  static DateTime? _parseDateTime(Object? value) => value == null
      ? null
      : DateTime.parse(value as String);
}