import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:blood_donation_network/core/secure_storage.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/auth/login_screen.dart';
import 'package:blood_donation_network/features/donor_profile/screens/donor_registration_screen.dart';
import 'package:blood_donation_network/features/home/home_screen.dart';
import 'package:blood_donation_network/main.dart';

/// In-memory SecureStorage so tests never touch the platform keychain.
class _FakeSecureStorage implements SecureStorage {
  final Map<String, String> _store = {};

  @override
  Future<void> saveSession({
    required String token,
    required String userId,
    String? donorProfileId,
    required String role,
    String? organizationId,
    String? email,
    String? fullName,
  }) async {
    _store
      ..['token'] = token
      ..['userId'] = userId
      ..['role'] = role;
    if (organizationId != null) _store['orgId'] = organizationId;
    if (email != null) _store['email'] = email;
    if (fullName != null) _store['fullName'] = fullName;
  }

  @override
  Future<
      ({
        String token,
        String userId,
        String? donorProfileId,
        String role,
        String? organizationId,
        String? email,
        String? fullName,
      })?> readSession() async {
    final token = _store['token'];
    if (token == null) return null;
    return (
      token: token,
      userId: _store['userId']!,
      donorProfileId: _store['donorProfileId'],
      role: _store['role']!,
      organizationId: _store['orgId'],
      email: _store['email'],
      fullName: _store['fullName'],
    );
  }

  @override
  Future<void> clear() async => _store.clear();
}

void main() {
  testWidgets('unauthenticated launch shows the login screen', (tester) async {
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          secureStorageProvider.overrideWithValue(_FakeSecureStorage()),
        ],
        child: const BloodDonationApp(),
      ),
    );

    // Restore runs in a microtask -> unauthenticated -> router redirects.
    await tester.pumpAndSettle();

    expect(find.byType(LoginScreen), findsOneWidget);
    expect(find.text('Sign in'), findsOneWidget);
  });

  testWidgets(
      'a staff session with no donor profile does NOT get trapped on donor '
      'registration (only donors are required to complete one)', (tester) async {
    final storage = _FakeSecureStorage();
    await storage.saveSession(
      token: 'staff-token',
      userId: 'staff-user-id',
      role: 'staff',
      // donorProfileId intentionally omitted — staff never has one.
    );

    await tester.pumpWidget(
      ProviderScope(
        overrides: [secureStorageProvider.overrideWithValue(storage)],
        child: const BloodDonationApp(),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.byType(DonorRegistrationScreen), findsNothing);
    expect(find.byType(HomeScreen), findsOneWidget);
  });

  test('AuthState defaults are coherent', () {
    const unknown = AuthState.unknown();
    const out = AuthState.signedOut();

    expect(unknown.status, AuthStatus.unknown);
    expect(unknown.isAuthenticated, isFalse);
    expect(out.status, AuthStatus.unauthenticated);
    expect(out.isAuthenticated, isFalse);
  });
}
