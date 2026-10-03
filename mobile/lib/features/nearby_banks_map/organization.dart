class Organization {
  const Organization({
    required this.id,
    required this.name,
    required this.type,
    required this.address,
    required this.latitude,
    required this.longitude,
    required this.phoneNumber,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String name;
  final String type;
  final String address;
  final double latitude;
  final double longitude;
  final String phoneNumber;
  final DateTime createdAt;
  final DateTime updatedAt;

  bool get isBloodBank {
    final normalized =
        type.trim().toLowerCase().replaceAll('_', '');
    return normalized == 'bloodbank';
  }

  bool get isHospital {
    return type.trim().toLowerCase() == 'hospital';
  }

  String get displayType {
    if (isBloodBank) return 'Blood Bank';
    if (isHospital) return 'Hospital';
    return type;
  }

  bool get hasValidCoordinates {
    return latitude >= -90 &&
        latitude <= 90 &&
        longitude >= -180 &&
        longitude <= 180 &&
        !(latitude == 0 && longitude == 0);
  }

  factory Organization.fromJson(Map<String, dynamic> json) {
    return Organization(
      id: (json['id'] ?? json['Id']).toString(),
      name: (json['name'] ?? json['Name'] ?? '').toString(),
      type: (json['type'] ?? json['Type'] ?? '').toString(),
      address: (json['address'] ?? json['Address'] ?? '').toString(),
      latitude: _toDouble(json['latitude'] ?? json['Latitude']),
      longitude: _toDouble(json['longitude'] ?? json['Longitude']),
      phoneNumber:
          (json['phoneNumber'] ?? json['PhoneNumber'] ?? '').toString(),
      createdAt:
          _toDateTime(json['createdAt'] ?? json['CreatedAt']),
      updatedAt:
          _toDateTime(json['updatedAt'] ?? json['UpdatedAt']),
    );
  }

  static double _toDouble(dynamic value) {
    if (value is num) {
      return value.toDouble();
    }

    return double.tryParse(value?.toString() ?? '') ?? 0.0;
  }

  static DateTime _toDateTime(dynamic value) {
    return DateTime.tryParse(value?.toString() ?? '') ??
        DateTime.fromMillisecondsSinceEpoch(0);
  }
}