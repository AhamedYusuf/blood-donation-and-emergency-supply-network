import 'dart:convert';

import 'package:blood_donation_network/core/api_client.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/notifications/devices_repository.dart';
import 'package:blood_donation_network/features/notifications/fcm_token_source.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

class _FakeTokenSource implements FcmTokenSource {
  _FakeTokenSource(this._token);
  final String? _token;
  @override
  Future<String?> getToken() async => _token;
  @override
  String get platform => 'android';
}

({DevicesRepository repo, List<http.Request> requests}) _harness({
  required String? fcmToken,
  String? bearer = 'jwt-1',
  http.Response Function(http.Request)? handler,
}) {
  final requests = <http.Request>[];
  final mock = MockClient((req) async {
    requests.add(req);
    return handler?.call(req) ?? http.Response('', 204);
  });

  final container = ProviderContainer(overrides: [
    apiClientProvider.overrideWithValue(ApiClient(httpClient: mock)),
    authTokenProvider.overrideWithValue(bearer),
    fcmTokenSourceProvider.overrideWithValue(_FakeTokenSource(fcmToken)),
  ]);
  addTearDown(container.dispose);
  return (repo: container.read(devicesRepositoryProvider), requests: requests);
}

void main() {
  test('registerCurrentDevice posts token + platform with the bearer', () async {
    final h = _harness(fcmToken: 'fcm-abc');

    await h.repo.registerCurrentDevice();

    final req = h.requests.single;
    expect(req.method, 'POST');
    expect(req.url.path, '/api/devices');
    expect(req.headers['Authorization'], 'Bearer jwt-1');
    expect(jsonDecode(req.body), {'fcmToken': 'fcm-abc', 'platform': 'android'});
  });

  test('registerCurrentDevice is a no-op when there is no FCM token yet', () async {
    final h = _harness(fcmToken: null);

    await h.repo.registerCurrentDevice();

    expect(h.requests, isEmpty);
  });

  test('registerCurrentDevice is a no-op when there is no session', () async {
    final h = _harness(fcmToken: 'fcm-abc', bearer: null);

    await h.repo.registerCurrentDevice();

    expect(h.requests, isEmpty);
  });

  test('unregisterCurrentDevice uses the explicit session token', () async {
    final h = _harness(fcmToken: 'fcm-abc', bearer: null);

    await h.repo.unregisterCurrentDevice(sessionToken: 'old-jwt');

    final req = h.requests.single;
    expect(req.url.path, '/api/devices/unregister');
    expect(req.headers['Authorization'], 'Bearer old-jwt');
    expect(jsonDecode(req.body), {'fcmToken': 'fcm-abc'});
  });

  test('sendTestNotification returns the parsed result body', () async {
    final h = _harness(
      fcmToken: 'fcm-abc',
      handler: (_) => http.Response(
          jsonEncode({'anyDelivered': true, 'delivered': 2}), 200,
          headers: {'content-type': 'application/json'}),
    );

    final result = await h.repo.sendTestNotification();

    expect(result['anyDelivered'], true);
    expect(result['delivered'], 2);
  });
}
