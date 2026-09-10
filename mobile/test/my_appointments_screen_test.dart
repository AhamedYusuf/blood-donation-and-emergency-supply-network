import 'dart:async';

import 'package:blood_donation_network/core/api_client.dart';
import 'package:blood_donation_network/features/appointments/appointment.dart';
import 'package:blood_donation_network/features/appointments/appointments_repository.dart';
import 'package:blood_donation_network/features/appointments/my_appointments_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

Appointment _appt({
  required String id,
  required String status,
  required DateTime when,
  String? workflowId,
}) =>
    Appointment(
      id: id,
      donorId: 'd1',
      organizationId: 'o1',
      relatedWorkflowId: workflowId,
      scheduledTime: when,
      status: status,
    );

Widget _host(List<Override> overrides) => ProviderScope(
      overrides: overrides,
      child: const MaterialApp(home: MyAppointmentsScreen()),
    );

void main() {
  testWidgets('shows a spinner while loading', (tester) async {
    final pending = Completer<List<Appointment>>();
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) => pending.future),
    ]));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    pending.complete(const []); // let the future settle so the test tears down cleanly
  });

  testWidgets('renders Upcoming and History sections from data', (tester) async {
    final now = DateTime.now();
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) async => [
            _appt(id: 'up', status: 'scheduled', when: now.add(const Duration(days: 2))),
            _appt(id: 'done', status: 'completed', when: now.subtract(const Duration(days: 5))),
          ]),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('UPCOMING'), findsOneWidget);
    expect(find.text('HISTORY'), findsOneWidget);
    expect(find.text('Scheduled'), findsOneWidget);
    expect(find.text('Completed'), findsOneWidget);
    // Cancel affordance only on the upcoming scheduled one.
    expect(find.byIcon(Icons.close), findsOneWidget);
  });

  testWidgets('shows the empty state when there are no appointments', (tester) async {
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith((ref) async => <Appointment>[]),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('No appointments yet'), findsOneWidget);
  });

  testWidgets('shows the error view with a retry button', (tester) async {
    await tester.pumpWidget(_host([
      myAppointmentsProvider.overrideWith(
        (ref) async => throw ApiException('Backend unreachable'),
      ),
    ]));
    await tester.pumpAndSettle();

    expect(find.text('Backend unreachable'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Retry'), findsOneWidget);
  });
}
