class Organization {
  const Organization({
    required this.id,
    required this.name,
    this.latitude,
    this.longitude,
  });

  final String id;
  final String name;
  final double? latitude;
  final double? longitude;

  factory Organization.fromJson(Map<String, dynamic> json) => Organization(
    id: json['id']?.toString() ?? '',
    name: json['name']?.toString() ?? '',
    latitude: _coordinate(json['latitude']),
    longitude: _coordinate(json['longitude']),
  );
}

double? _coordinate(Object? value) =>
    value is num ? value.toDouble() : double.tryParse('$value');
