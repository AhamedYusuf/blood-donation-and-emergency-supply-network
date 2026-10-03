import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../auth/auth_controller.dart';
import 'notification.dart';

class NotificationInboxRepository {
  NotificationInboxRepository(this._ref);

  final Ref _ref;

  ApiClient get _api => _ref.read(apiClientProvider);
  String? get _token => _ref.read(authTokenProvider);

  /// GET /api/notifications — the caller's own inbox, newest first.
  Future<List<AppNotification>> list({int page = 1, int pageSize = 20}) async {
    final json = await _api.get(
      '/api/notifications?page=$page&pageSize=$pageSize',
      token: _token,
    );
    final items = (json as Map<String, dynamic>)['items'] as List;
    return items.cast<Map<String, dynamic>>().map(AppNotification.fromJson).toList();
  }

  /// GET /api/notifications/unread-count — for a badge.
  Future<int> unreadCount() async {
    final json = await _api.get('/api/notifications/unread-count', token: _token);
    return json as int;
  }

  /// POST /api/notifications/{id}/read.
  Future<void> markAsRead(String id) async {
    await _api.post('/api/notifications/$id/read', token: _token);
  }
}

final notificationInboxRepositoryProvider =
    Provider<NotificationInboxRepository>((ref) => NotificationInboxRepository(ref));

/// The signed-in donor's notifications. Auto-disposed so it refetches
/// when the screen is revisited; `ref.invalidate` forces an immediate
/// refresh (e.g. after marking one as read).
final notificationInboxProvider =
    FutureProvider.autoDispose<List<AppNotification>>((ref) {
  return ref.watch(notificationInboxRepositoryProvider).list();
});

final unreadNotificationCountProvider = FutureProvider.autoDispose<int>((ref) {
  return ref.watch(notificationInboxRepositoryProvider).unreadCount();
});
