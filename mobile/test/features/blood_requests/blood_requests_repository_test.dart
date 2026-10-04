import 'dart:convert';

import 'package:blood_donation_network/core/api_client.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/blood_requests/blood_requests_repository.dart';
import 'package:blood_donation_network/features/donor_profile/models/donor_profile.dart';
import 'package:blood_donation_network/features/donor_profile/providers/donor_profile_provider.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

class _FakeAuthController extends AuthController {
  _FakeAuthController(this._state);

  final AuthState _state;

  @override
  AuthState build() => _state;
}

({
  BloodRequestsRepository repository,
  List<http.Request> requests,
  ProviderContainer container,
})
_harness({
  required String role,
  required DonorProfile? profile,
  required http.Response Function(http.Request) response,
}) {
  final requests = <http.Request>[];
  final client = MockClient((request) async {
    requests.add(request);
    return response(request);
  });
  final container = ProviderContainer(
    overrides: [
      apiClientProvider.overrideWithValue(
        ApiClient(httpClient: client, baseUrl: 'https://api.example.test'),
      ),
      authControllerProvider.overrideWith(
        () => _FakeAuthController(
          AuthState(
            status: AuthStatus.authenticated,
            token: 'test-token',
            donorProfileId: role == 'donor' ? 'profile-1' : null,
            role: role,
          ),
        ),
      ),
      donorProfileProvider.overrideWith((ref) async => profile),
    ],
  );
  addTearDown(container.dispose);

  return (
    repository: container.read(bloodRequestsRepositoryProvider),
    requests: requests,
    container: container,
  );
}

http.Response _requestResponse() => http.Response(
  jsonEncode([
    {
      'id': 'request-1',
      'requesterId': 'user-1',
      'organizationId': 'org-1',
      'bloodType': 'O+',
      'unitsRequested': 2,
      'urgency': 'urgent',
      'status': 'open',
      'hospitalName': 'Central Hospital',
      'latitude': 24,
      'longitude': 67.5,
      'notes': 'Emergency supply needed',
      'createdAt': '2026-09-25T10:00:00Z',
      'fulfilledAt': null,
      'closedAt': null,
    },
  ]),
  200,
  headers: {'content-type': 'application/json'},
);

DonorProfile _profile({double? latitude, double? longitude}) => DonorProfile(
  id: 'profile-1',
  userId: 'user-1',
  fullName: 'Test Donor',
  bloodType: 'O+',
  eligibilityStatus: 'eligible',
  dateOfBirth: DateTime(1990),
  latitude: latitude,
  longitude: longitude,
  locationVerified: true,
  verifiedByAdmin: true,
);

void main() {
  test('donor requests use stored coordinates and a 50 km radius', () async {
    final h = _harness(
      role: 'donor',
      profile: _profile(latitude: 24.123456, longitude: -67.654321),
      response: (_) => _requestResponse(),
    );

    final requests = await h.repository.getRequests();

    expect(requests, hasLength(1));
    expect(requests.single.id, 'request-1');
    expect(requests.single.latitude, 24.0);
    expect(requests.single.longitude, 67.5);

    final request = h.requests.single;
    expect(request.method, 'GET');
    expect(request.url.path, '/api/requests');
    expect(request.url.queryParameters, {
      'nearLat': '24.123456',
      'nearLng': '-67.654321',
      'radiusKm': '50',
    });
  });

  for (final role in ['staff', 'admin']) {
    test('$role requests do not include donor location parameters', () async {
      final h = _harness(
        role: role,
        profile: null,
        response: (_) => _requestResponse(),
      );

      final requests = await h.repository.getRequests();

      expect(requests, hasLength(1));
      expect(h.requests.single.url.path, '/api/requests');
      expect(h.requests.single.url.queryParameters, isEmpty);
    });
  }

  test(
    'donor without latitude gets a clear error without making a request',
    () async {
      final h = _harness(
        role: 'donor',
        profile: _profile(longitude: 67),
        response: (_) => _requestResponse(),
      );

      await expectLater(
        h.repository.getRequests(),
        throwsA(
          isA<ApiException>().having(
            (error) => error.message,
            'message',
            contains('donor location is unavailable'),
          ),
        ),
      );
      expect(h.requests, isEmpty);
    },
  );

  test(
    'donor without longitude gets a clear error without making a request',
    () async {
      final h = _harness(
        role: 'donor',
        profile: _profile(latitude: 24),
        response: (_) => _requestResponse(),
      );

      await expectLater(
        h.repository.getRequests(),
        throwsA(
          isA<ApiException>().having(
            (error) => error.message,
            'message',
            contains('donor location is unavailable'),
          ),
        ),
      );
      expect(h.requests, isEmpty);
    },
  );
}
