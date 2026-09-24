class DonorProfile {
  const DonorProfile({
    required this.id,
    required this.userId,
    required this.fullName,
    required this.bloodType,
    required this.eligibilityStatus,
    required this.dateOfBirth,
    this.lastDonationDate,
    this.address,
    this.medicalFlags = const {},
    this.latitude,
    this.longitude,
    required this.locationVerified,
    required this.verifiedByAdmin,
  });

  final String id;
  final String userId;
  final String fullName;
  final String bloodType;
  final String eligibilityStatus;
  final DateTime dateOfBirth;
  final DateTime? lastDonationDate;
  final String? address;
  final Map<String, bool> medicalFlags;
  final double? latitude;
  final double? longitude;
  final bool locationVerified;
  final bool verifiedByAdmin;

  factory DonorProfile.fromJson(Map<String, dynamic> json) => DonorProfile(
        id: json['id'] as String,
        userId: json['userId'] as String,
        fullName: json['fullName'] as String? ?? '',
        bloodType: json['bloodType'] as String,
        eligibilityStatus: json['eligibilityStatus'] as String,
        dateOfBirth: DateTime.parse(json['dateOfBirth'] as String),
        lastDonationDate: json['lastDonationDate'] == null
            ? null
            : DateTime.parse(json['lastDonationDate'] as String),
        address: json['address'] as String?,
        medicalFlags: Map<String, bool>.from(json['medicalFlags'] as Map? ?? {}),
        latitude: (json['latitude'] as num?)?.toDouble(),
        longitude: (json['longitude'] as num?)?.toDouble(),
        locationVerified: json['locationVerified'] as bool? ?? false,
        verifiedByAdmin: json['verifiedByAdmin'] as bool? ?? false,
      );
}