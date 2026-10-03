import 'dart:convert';

import 'package:blood_donation_network/core/api_client.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/notifications/notification_inbox_repository.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

({NotificationInboxRepository repo, List<http.Request> requests})
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
    repo: container.read(notificationInboxRepositoryProvider),
    requests: requests,
  );
}

http.Response _json(Object body, [int status = 200]) =>
    http.Response(jsonEncode(body), status,
        headers: {'content-type': 'application/json'});

void main() {
  test('list GETs a page and parses items, attaching the token', () async {
    final h = _harness((_) => _json({
          'items': [
            {
              'id': 'n1',
              'title': 'Urgent request matches your type',
              'body': 'Open the app to confirm or reschedule.',
              'data': {'appointmentId': 'a1'},
              'createdAt': '2026-09-01T10:00:00Z',
              'readAt': null,
            },
          ],
          'page': 1,
          'pageSize': 20,
          'totalCount': 1,
        }));

    final result = await h.repo.list();

    expect(result, hasLength(1));
    expect(result.first.title, 'Urgent request matches your type');
    expect(result.first.appointmentId, 'a1');
    expect(result.first.isUnread, isTrue);

    final req = h.requests.single;
    expect(req.method, 'GET');
    expect(req.url.path, '/api/notifications');
    expect(req.url.query, 'page=1&pageSize=20');
    expect(req.headers['Authorization'], 'Bearer test-token');
  });

  test('a read notification parses readAt and isUnread is false', () async {
    final h = _harness((_) => _json({
          'items': [
            {
              'id': 'n1',
              'title': 't',
              'body': 'b',
              'data': null,
              'createdAt': '2026-09-01T10:00:00Z',
              'readAt': '2026-09-01T11:00:00Z',
            },
          ],
          'page': 1,
          'pageSize': 20,
          'totalCount': 1,
        }));

    final result = await h.repo.list();

    expect(result.single.isUnread, isFalse);
    expect(result.single.appointmentId, isNull);
  });

  test('unreadCount GETs the badge count', () async {
    final h = _harness((_) => http.Response('3', 200,
        headers: {'content-type': 'application/json'}));

    final count = await h.repo.unreadCount();

    expect(count, 3);
    expect(h.requests.single.url.path, '/api/notifications/unread-count');
  });

  test('markAsRead POSTs to /{id}/read', () async {
    final h = _harness((_) => http.Response('', 204));

    await h.repo.markAsRead('n1');

    final req = h.requests.single;
    expect(req.method, 'POST');
    expect(req.url.path, '/api/notifications/n1/read');
  });
}
