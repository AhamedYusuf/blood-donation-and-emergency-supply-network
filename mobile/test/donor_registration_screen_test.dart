import 'package:blood_donation_network/core/age.dart';
import 'package:blood_donation_network/core/api_client.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/donor_profile/screens/donor_registration_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

// Covers the two real mismatches this screen had against the web app's
// DonorProfilePage.tsx: the minimum age was 16 here vs 18 on web, and
// only one of the five medical screening flags (recent illness) was
// ever collected or sent to the backend.
void main() {
  Future<void> pumpScreen(WidgetTester tester) async {
    tester.view.physicalSize = const Size(800, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(() {
      tester.view.resetPhysicalSize();
      tester.view.resetDevicePixelRatio();
    });

    final mock = MockClient((req) async => http.Response('', 400));

    await tester.pumpWidget(ProviderScope(
      overrides: [
        apiClientProvider.overrideWithValue(ApiClient(httpClient: mock)),
        authTokenProvider.overrideWithValue('test-token'),
      ],
      child: const MaterialApp(home: DonorRegistrationScreen()),
    ));
  }

  group('isAtLeastYearsOld (the age rule this screen relies on)', () {
    test('matches web: 18 years old today passes, 17 does not', () {
      final now = DateTime(2026, 6, 15);
      expect(isAtLeastYearsOld(DateTime(2008, 6, 15), 18, now: now), isTrue);
      expect(isAtLeastYearsOld(DateTime(2008, 6, 16), 18, now: now), isFalse);
      expect(isAtLeastYearsOld(DateTime(2009, 6, 15), 18, now: now), isFalse);
    });

    test('a 16 or 17 year old is no longer accepted (was the mobile-only bug)', () {
      final now = DateTime(2026, 1, 1);
      final seventeenYearsAgo = DateTime(2008, 6, 1);
      expect(isAtLeastYearsOld(seventeenYearsAgo, 18, now: now), isFalse);
      expect(isAtLeastYearsOld(seventeenYearsAgo, 16, now: now), isTrue); // the old, wrong rule
    });
  });

  testWidgets('shows all five medical screening questions, not just recent illness', (tester) async {
    await pumpScreen(tester);

    expect(find.text('Recent illness or infection'), findsOneWidget);
    expect(find.text('Recent surgery (within 6 months)'), findsOneWidget);
    expect(find.text('Chronic condition (e.g. diabetes, heart disease)'), findsOneWidget);
    expect(find.text('HIV positive'), findsOneWidget);
    expect(find.text('Hepatitis B or C'), findsOneWidget);
  });

  testWidgets('the age-requirement message says 18, not 16', (tester) async {
    await pumpScreen(tester);

    expect(find.text('You must be at least 18 years old to register'), findsNothing);
    expect(find.text('You must be at least 16 years old'), findsNothing);
  });

  testWidgets('requires a last-donation date only when "donated before" is on', (tester) async {
    await pumpScreen(tester);

    expect(find.text('Last donation date'), findsNothing);

    await tester.tap(find.text('Have you donated blood before?'));
    await tester.pumpAndSettle();

    expect(find.text('Last donation date'), findsOneWidget);
    expect(find.text('Last donation date is required'), findsOneWidget);

    await tester.tap(find.text('Have you donated blood before?'));
    await tester.pumpAndSettle();

    expect(find.text('Last donation date'), findsNothing);
  });

  testWidgets('medical flag switches toggle independently', (tester) async {
    await pumpScreen(tester);

    final hepatitisSwitch = find.widgetWithText(SwitchListTile, 'Hepatitis B or C');
    final illnessSwitch = find.widgetWithText(SwitchListTile, 'Recent illness or infection');

    expect(tester.widget<SwitchListTile>(hepatitisSwitch).value, isFalse);
    expect(tester.widget<SwitchListTile>(illnessSwitch).value, isFalse);

    await tester.tap(hepatitisSwitch);
    await tester.pumpAndSettle();

    expect(tester.widget<SwitchListTile>(hepatitisSwitch).value, isTrue);
    expect(tester.widget<SwitchListTile>(illnessSwitch).value, isFalse);
  });
}
