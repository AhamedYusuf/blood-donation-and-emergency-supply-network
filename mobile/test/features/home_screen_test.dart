import 'package:blood_donation_network/features/appointments/appointment.dart';
import 'package:blood_donation_network/features/appointments/appointments_repository.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/blood_requests/blood_request.dart';
import 'package:blood_donation_network/features/blood_requests/blood_requests_repository.dart';
import 'package:blood_donation_network/features/donor_profile/eligibility_text.dart';
import 'package:blood_donation_network/features/donor_profile/models/eligibility_response.dart';
import 'package:blood_donation_network/features/donor_profile/providers/donor_profile_provider.dart';
import 'package:blood_donation_network/features/home/home_screen.dart';
import 'package:blood_donation_network/features/notifications/notification_inbox_repository.dart';
import 'package:blood_donation_network/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _FakeAuth extends AuthController {
  _FakeAuth(this.role);
  final String role;
  @override
  AuthState build() => AuthState(status: AuthStatus.authenticated, role: role, fullName: 'Test User');
}

Widget _host(String role) => ProviderScope(
      overrides: [
        authControllerProvider.overrideWith(() => _FakeAuth(role)),
        myAppointmentsProvider.overrideWith((ref) async => <Appointment>[]),
        unreadNotificationCountProvider.overrideWith((ref) async => 0),
        eligibilityProvider.overrideWith(
          (ref) async => const EligibilityResponse(isEligible: false, daysUntilEligible: 12),
        ),
        bloodRequestsProvider.overrideWith((ref) async => <BloodRequest>[]),
      ],
      child: MaterialApp(theme: buildAppTheme(), home: const HomeScreen()),
    );

void main() {
  testWidgets('donor home has only working actions and no "coming soon" section', (tester) async {
    await tester.pumpWidget(_host('donor'));
    await tester.pumpAndSettle();

    expect(find.textContaining('oming soon'), findsNothing);
    expect(find.text('Requests near you'), findsOneWidget);
    expect(find.text('My eligibility'), findsOneWidget);
    expect(find.text('You can donate again in 12 days'), findsOneWidget);
    expect(find.text('Nearby blood banks'), findsOneWidget);
  });

  testWidgets('staff home shows request actions instead of donor ones', (tester) async {
    await tester.pumpWidget(_host('staff'));
    await tester.pumpAndSettle();

    expect(find.text('New blood request'), findsOneWidget);
    expect(find.text('All requests'), findsOneWidget);
    expect(find.text('My donations'), findsNothing);
    expect(find.text('Book a donation'), findsNothing);
  });

  test('eligibility reasons are explained in plain language', () {
    expect(eligibilityExplanation('verified_by_admin = false'), contains('verify your donor profile'));
    expect(eligibilityExplanation('last_donation_date within 90 days (12 days remaining)'), contains('90 days'));
    expect(eligibilityExplanation('medical_flags.recent_illness = true'), contains('recent illness'));
    expect(eligibilitySummary(const EligibilityResponse(isEligible: true)), 'You can donate now');
  });
}
