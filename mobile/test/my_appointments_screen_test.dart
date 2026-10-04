import 'dart:async';
import 'dart:convert';

import 'package:blood_donation_network/core/api_client.dart';
import 'package:blood_donation_network/features/appointments/appointment.dart';
import 'package:blood_donation_network/features/appointments/appointments_repository.dart';
import 'package:blood_donation_network/features/appointments/my_appointments_screen.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/theme/app_theme.dart';
import 'package:blood_donation_network/widgets/states.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

Appointment _appt({
  required String id,
  required String status,
  required DateTime when,
  String? workflowId,
  int? units,
}) =>
    Appointment(
      id: id,
      donorId: 'd1',
      organizationId: 'o1',
      relatedWorkflowId: workflowId,
      scheduledTime: when,
      status: status,
      donorBloodType: 'O+',
      unitsDonated: units,
    );

Widget _host(List<Override> overrides) => ProviderScope(
      overrides: overrides,
      child: MaterialApp(theme: buildAppTheme(), home: const MyAppointmentsScreen()),
    );

void main() {
  testWidgets('shows the skeleton while loading', (tester) async {
    final pending = Completer<List<Appointment>>();
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) => pending.future),
    ]));
    await tester.pump();

    expect(find.byType(AppointmentsSkeleton), findsOneWidget);
    pending.complete(const []);
  });

  testWidgets('renders the pending and completed donation sections', (tester) async {
    final now = DateTime.now();
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) async => [
            _appt(id: 'up', status: 'scheduled', when: now.add(const Duration(days: 2)), workflowId: 'w1'),
            _appt(id: 'done', status: 'completed', when: now.subtract(const Duration(days: 5)), units: 1),
          ]),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('NEXT DONATION'), findsOneWidget);
    expect(find.text('COMPLETED DONATIONS'), findsOneWidget);
    expect(find.text('Scheduled'), findsOneWidget);
    expect(find.text('Completed'), findsOneWidget);
    expect(find.text('Agent match'), findsOneWidget); // agent tag on the hero
    expect(find.text('Cancel'), findsOneWidget); // only on the cancellable hero
    expect(find.text('Reschedule'), findsOneWidget);
  });

  testWidgets('when nothing is upcoming, the hero slot invites a booking', (tester) async {
    final now = DateTime.now();
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) async => [
            _appt(id: 'old', status: 'completed', when: now.subtract(const Duration(days: 20)), units: 2),
          ]),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('Nothing booked'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Book a donation'), findsOneWidget);
    expect(find.text('Cancel'), findsNothing);
  });

  testWidgets('empty state offers a first booking', (tester) async {
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) async => <Appointment>[]),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('No donations booked'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Book a donation'), findsOneWidget);
    expect(find.text('Book'), findsNothing);
  });

  testWidgets('a pending appointment offers Accept/Reschedule, not Cancel', (tester) async {
    final now = DateTime.now();
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) async => [
            _appt(
                id: 'pending1',
                status: 'pending_confirmation',
                when: now.add(const Duration(days: 1)),
                workflowId: 'w1'),
          ]),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('Awaiting you'), findsOneWidget);
    expect(find.text('Accept'), findsOneWidget);
    expect(find.text('Reschedule'), findsOneWidget);
    expect(find.text('Cancel'), findsNothing);
  });

  testWidgets('accepting a pending appointment calls confirm and shows a confirmation', (tester) async {
    final now = DateTime.now();
    final mock = MockClient((req) async {
      expect(req.method, 'POST');
      expect(req.url.path, '/api/appointments/pending1/confirm');
      return http.Response(
        jsonEncode({
          'id': 'pending1',
          'donorId': 'd1',
          'organizationId': 'o1',
          'relatedWorkflowId': 'w1',
          'scheduledTime': now.add(const Duration(days: 1)).toUtc().toIso8601String(),
          'status': 'scheduled',
          'donorBloodType': 'O+',
          'unitsDonated': null,
        }),
        200,
        headers: {'content-type': 'application/json'},
      );
    });

    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) async => [
            _appt(
                id: 'pending1',
                status: 'pending_confirmation',
                when: now.add(const Duration(days: 1)),
                workflowId: 'w1'),
          ]),
      apiClientProvider.overrideWithValue(ApiClient(httpClient: mock)),
      authTokenProvider.overrideWithValue('test-token'),
    ]));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Accept'));
    await tester.pumpAndSettle();

    expect(find.text('Appointment confirmed'), findsOneWidget);
  });

  testWidgets('error state shows the message and a retry', (tester) async {
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) async => throw ApiException('Backend unreachable')),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('Backend unreachable'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Try again'), findsOneWidget);
  });
}
