import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../auth/auth_controller.dart';
import 'fcm_token_source.dart';

/// Talks to the backend's `/api/devices` endpoints so the server knows
/// where to push this donor's alerts. All calls are best-effort — a
/// failure here must never block login/logout.
class DevicesRepository {
  DevicesRepository(this._ref);

  final Ref _ref;

  ApiClient get _api => _ref.read(apiClientProvider);
  String? get _token => _ref.read(authTokenProvider);
  FcmTokenSource get _fcm => _ref.read(fcmTokenSourceProvider);

  /// Registers this device's current FCM token. No-op if there's no token
  /// yet (Firebase not set up) or no session.
  Future<void> registerCurrentDevice() async {
    final token = _token;
    if (token == null) return;

    final fcmToken = await _fcm.getToken();
    if (fcmToken == null || fcmToken.isEmpty) return;

    await _api.post(
      '/api/devices',
      token: token,
      body: {'fcmToken': fcmToken, 'platform': _fcm.platform},
    );
  }

  /// Removes this device's token (called on logout). Pass the session token
  /// explicitly since the caller may be about to clear it.
  Future<void> unregisterCurrentDevice({String? sessionToken}) async {
    final token = sessionToken ?? _token;
    if (token == null) return;

    final fcmToken = await _fcm.getToken();
    if (fcmToken == null || fcmToken.isEmpty) return;

    await _api.post(
      '/api/devices/unregister',
      token: token,
      body: {'fcmToken': fcmToken},
    );
  }

  /// Asks the backend to push a test alert to this donor's devices.
  Future<Map<String, dynamic>> sendTestNotification() async {
    final json = await _api.post('/api/devices/test', token: _token);
    return (json as Map).cast<String, dynamic>();
  }
}

final devicesRepositoryProvider =
    Provider<DevicesRepository>((ref) => DevicesRepository(ref));
