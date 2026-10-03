/// Mirrors the backend DonorNotificationDto — the durable inbox record
/// written alongside every push attempt, regardless of whether the push
/// itself could be delivered.
class AppNotification {
  AppNotification({
    required this.id,
    required this.title,
    required this.body,
    required this.createdAt,
    this.data,
    this.readAt,
  });

  final String id;
  final String title;
  final String body;
  final Map<String, dynamic>? data;
  final DateTime createdAt;
  final DateTime? readAt;

  bool get isUnread => readAt == null;

  /// Set when this notification is about a specific appointment (e.g. an
  /// agent dispatch) — lets the inbox deep-link into My Appointments.
  String? get appointmentId => data?['appointmentId'] as String?;

  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
        id: json['id'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        data: (json['data'] as Map?)?.cast<String, dynamic>(),
        createdAt: DateTime.parse(json['createdAt'] as String).toLocal(),
        readAt: json['readAt'] != null
            ? DateTime.parse(json['readAt'] as String).toLocal()
            : null,
      );
}
