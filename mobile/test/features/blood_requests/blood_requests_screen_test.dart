import 'dart:async';

import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/blood_requests/blood_request.dart';
import 'package:blood_donation_network/features/blood_requests/blood_requests_screen.dart';
import 'package:blood_donation_network/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeAuthController extends AuthController {
  @override
  AuthState build() =>
      const AuthState(status: AuthStatus.authenticated, role: 'donor');
}

BloodRequest _request() => BloodRequest(
  id: 'request-1',
  requesterId: 'user-1',
  organizationId: 'org-1',
  bloodType: 'O+',
  unitsRequested: 3,
  urgency: 'urgent',
  status: 'open',
  hospitalName: 'Central Hospital',
  latitude: 24,
  longitude: 67,
  notes: '',
  createdAt: DateTime(2026, 9, 25),
);

Widget _host(Override requestOverride) => ProviderScope(
  overrides: [
    requestOverride,
    authControllerProvider.overrideWith(_FakeAuthController.new),
  ],
  child: MaterialApp(theme: buildAppTheme(), home: const BloodRequestsScreen()),
);

void main() {
  testWidgets('shows loading state while requests are pending', (tester) async {
    final pending = Completer<List<BloodRequest>>();
    await tester.pumpWidget(
      _host(bloodRequestsProvider.overrideWith((ref) => pending.future)),
    );
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    pending.complete(const []);
  });

  testWidgets('shows the empty state when there are no requests', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(
        bloodRequestsProvider.overrideWith((ref) async => <BloodRequest>[]),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('No blood requests'), findsOneWidget);
    expect(
      find.text('There are no blood requests to show right now.'),
      findsOneWidget,
    );
  });

  testWidgets('renders blood type, hospital, and status for a request', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(bloodRequestsProvider.overrideWith((ref) async => [_request()])),
    );
    await tester.pumpAndSettle();

    expect(find.text('Central Hospital'), findsOneWidget);
    expect(find.text('O+'), findsOneWidget);
    expect(find.text('open'), findsOneWidget);
  });
}
