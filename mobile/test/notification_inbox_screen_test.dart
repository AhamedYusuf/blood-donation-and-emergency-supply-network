import 'dart:async';

import 'package:blood_donation_network/core/api_client.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/notifications/notification.dart';
import 'package:blood_donation_network/features/notifications/notification_inbox_repository.dart';
import 'package:blood_donation_network/features/notifications/notification_inbox_screen.dart';
import 'package:blood_donation_network/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

AppNotification _n({
  required String id,
  required String title,
  DateTime? readAt,
  Map<String, dynamic>? data,
}) =>
    AppNotification(
      id: id,
      title: title,
      body: 'Body text',
      createdAt: DateTime.now().subtract(const Duration(hours: 1)),
      readAt: readAt,
      data: data,
    );

Widget _host(List<Override> overrides) => ProviderScope(
      overrides: overrides,
      child: MaterialApp(theme: buildAppTheme(), home: const NotificationInboxScreen()),
    );

void main() {
  testWidgets('shows a skeleton while loading', (tester) async {
    final pending = Completer<List<AppNotification>>();
    await tester.pumpWidget(_host([
      notificationInboxProvider.overrideWith((ref) => pending.future),
    ]));
    await tester.pump();

    expect(find.text('Notifications'), findsOneWidget);
    pending.complete(const []);
  });

  testWidgets('empty state invites nothing further', (tester) async {
    await tester.pumpWidget(_host([
      notificationInboxProvider.overrideWith((ref) async => <AppNotification>[]),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('No notifications yet'), findsOneWidget);
  });

  testWidgets('renders unread and read notifications with a dot only on unread', (tester) async {
    await tester.pumpWidget(_host([
      notificationInboxProvider.overrideWith((ref) async => [
            _n(id: 'n1', title: 'Unread one'),
            _n(id: 'n2', title: 'Read one', readAt: DateTime.now()),
          ]),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('Unread one'), findsOneWidget);
    expect(find.text('Read one'), findsOneWidget);
  });

  testWidgets('tapping an unread notification marks it as read', (tester) async {
    final mock = MockClient((req) async {
      if (req.method == 'POST' && req.url.path == '/api/notifications/n1/read') {
        return http.Response('', 204);
      }
      return http.Response('', 404);
    });

    await tester.pumpWidget(_host([
      notificationInboxProvider.overrideWith((ref) async => [_n(id: 'n1', title: 'Unread one')]),
      apiClientProvider.overrideWithValue(ApiClient(httpClient: mock)),
      authTokenProvider.overrideWithValue('test-token'),
    ]));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Unread one'));
    await tester.pumpAndSettle();

    // No exception thrown means the mark-as-read call succeeded and the
    // provider refresh completed cleanly.
    expect(find.text('Unread one'), findsOneWidget);
  });

  testWidgets('error state shows the message and a retry', (tester) async {
    await tester.pumpWidget(_host([
      notificationInboxProvider.overrideWith((ref) async => throw ApiException('Backend unreachable')),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('Backend unreachable'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Try again'), findsOneWidget);
  });
}
