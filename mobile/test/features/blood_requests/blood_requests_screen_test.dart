import 'dart:async';

import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/blood_requests/blood_request.dart';
import 'package:blood_donation_network/features/blood_requests/blood_requests_repository.dart';
import 'package:blood_donation_network/features/blood_requests/blood_requests_screen.dart';
import 'package:blood_donation_network/features/donor_profile/models/eligibility_response.dart';
import 'package:blood_donation_network/features/donor_profile/providers/donor_profile_provider.dart';
import 'package:blood_donation_network/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeAuthController extends AuthController {
  _FakeAuthController(this.role);
  final String role;
  @override
  AuthState build() =>
      AuthState(status: AuthStatus.authenticated, role: role);
}

BloodRequest _request({double? distanceKm}) => BloodRequest(
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
  distanceKm: distanceKm,
);

Widget _host(
  Override requestOverride, {
  String role = 'donor',
  EligibilityResponse eligibility = const EligibilityResponse(isEligible: true),
}) => ProviderScope(
  overrides: [
    requestOverride,
    authControllerProvider.overrideWith(() => _FakeAuthController(role)),
    eligibilityProvider.overrideWith((ref) async => eligibility),
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

  testWidgets('an eligible donor with nothing nearby is told requests will appear', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(
        bloodRequestsProvider.overrideWith((ref) async => <BloodRequest>[]),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Requests near you'), findsOneWidget);
    expect(find.text('No requests near you'), findsOneWidget);
  });

  testWidgets('a donor who cannot donate yet sees why the list is empty', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(
        bloodRequestsProvider.overrideWith((ref) async => <BloodRequest>[]),
        eligibility: const EligibilityResponse(
          isEligible: false,
          reason: 'verified_by_admin = false',
        ),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('You can’t donate right now'), findsOneWidget);
    expect(find.text('See my eligibility'), findsOneWidget);
  });

  testWidgets('a donor sees the hospital, blood type and distance, not the internal status', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(bloodRequestsProvider.overrideWith((ref) async => [_request(distanceKm: 2.46)])),
    );

    await tester.pumpAndSettle();

    expect(find.text('Central Hospital'), findsOneWidget);
    expect(find.text('O+'), findsOneWidget);
    expect(find.text('2.5 km away'), findsOneWidget);
    expect(find.text('Open'), findsNothing);
  });

  testWidgets('staff see the status overview and each request status', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(
        bloodRequestsProvider.overrideWith((ref) async => [_request()]),
        role: 'staff',
      ),
    );

    await tester.pumpAndSettle();

    expect(find.text('Central Hospital'), findsOneWidget);
    expect(find.text('O+'), findsOneWidget);
    expect(find.text('Open'), findsNWidgets(2));
  });
}
