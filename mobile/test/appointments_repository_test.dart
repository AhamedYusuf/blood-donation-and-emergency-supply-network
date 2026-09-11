import 'dart:convert';

import 'package:blood_donation_network/core/api_client.dart';
import 'package:blood_donation_network/features/appointments/appointments_repository.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

/// Builds a container whose ApiClient is backed by [handler], with a fixed
/// bearer token, and returns the repository plus a log of requests seen.
({AppointmentsRepository repo, List<http.Request> requests, ProviderContainer container})
    _harness(http.Response Function(http.Request) handler) {
  final requests = <http.Request>[];
  final mock = MockClient((req) async {
    requests.add(req);
    return handler(req);
  });

  final container = ProviderContainer(overrides: [
    apiClientProvider.overrideWithValue(ApiClient(httpClient: mock)),
    authTokenProvider.overrideWithValue('test-token'),
  ]);
  addTearDown(container.dispose);

  return (
    repo: container.read(appointmentsRepositoryProvider),
    requests: requests,
    container: container,
  );
}

http.Response _json(Object body, [int status = 200]) =>
    http.Response(jsonEncode(body), status,
        headers: {'content-type': 'application/json'});

void main() {
  test('myAppointments parses and sorts newest-first, attaching the token', () async {
    final h = _harness((_) => _json([
          {
            'id': 'a1',
            'donorId': 'd1',
            'organizationId': 'o1',
            'relatedWorkflowId': null,
            'scheduledTime': '2026-01-01T10:00:00Z',
            'status': 'completed',
            'donorBloodType': 'O+',
            'unitsDonated': 1,
          },
          {
            'id': 'a2',
            'donorId': 'd1',
            'organizationId': 'o1',
            'relatedWorkflowId': 'w9',
            'scheduledTime': '2026-06-01T10:00:00Z',
            'status': 'scheduled',
            'donorBloodType': 'O+',
            'unitsDonated': null,
          },
        ]));

    final result = await h.repo.myAppointments('d1');

    expect(result.map((a) => a.id), ['a2', 'a1']); // newest first
    expect(result.first.isAgentMatched, isTrue);
    expect(result.last.unitsDonated, 1);

    final req = h.requests.single;
    expect(req.method, 'GET');
    expect(req.url.path, '/api/appointments/donor/d1');
    expect(req.headers['Authorization'], 'Bearer test-token');
  });

  test('book sends organizationId + UTC ISO time + null workflow', () async {
    final h = _harness((_) => _json({
          'id': 'new',
          'donorId': 'd1',
          'organizationId': 'o1',
          'relatedWorkflowId': null,
          'scheduledTime': '2026-09-20T09:30:00Z',
          'status': 'scheduled',
          'donorBloodType': null,
          'unitsDonated': null,
        }));

    final localTime = DateTime(2026, 9, 20, 15, 0); // local
    await h.repo.book(organizationId: 'o1', scheduledTime: localTime);

    final req = h.requests.single;
    expect(req.method, 'POST');
    expect(req.url.path, '/api/appointments');
    final sent = jsonDecode(req.body) as Map<String, dynamic>;
    expect(sent['organizationId'], 'o1');
    expect(sent['relatedWorkflowId'], isNull);
    expect(sent['scheduledTime'], endsWith('Z')); // serialised as UTC
    expect(DateTime.parse(sent['scheduledTime'] as String).toUtc(),
        localTime.toUtc());
  });

  test('cancel PUTs newStatus=cancelled', () async {
    final h = _harness((_) => http.Response('', 204));

    await h.repo.cancel('a5');

    final req = h.requests.single;
    expect(req.method, 'PUT');
    expect(req.url.path, '/api/appointments/a5/status');
    expect(jsonDecode(req.body), {'newStatus': 'cancelled'});
  });

  test('bloodBanks keeps only blood_bank organizations', () async {
    final h = _harness((_) => _json([
          {'id': 'o1', 'name': 'City Blood Bank', 'address': 'A', 'type': 'blood_bank'},
          {'id': 'o2', 'name': 'General Hospital', 'address': 'B', 'type': 'hospital'},
          {'id': 'o3', 'name': 'Red Cross', 'address': 'C', 'type': 'Blood_Bank'},
        ]));

    final banks = await h.repo.bloodBanks();

    expect(banks.map((b) => b.id), ['o1', 'o3']);
  });

  test('non-2xx surfaces as ApiException with the ProblemDetails message', () async {
    final h = _harness((_) => _json({
          'title': 'Validation failed',
          'errors': {
            'scheduledTime': ['scheduledTime must be in the future']
          }
        }, 400));

    expect(
      () => h.repo.book(
          organizationId: 'o1', scheduledTime: DateTime(2030)),
      throwsA(isA<ApiException>().having(
          (e) => e.message, 'message', 'scheduledTime must be in the future')),
    );
  });
}
