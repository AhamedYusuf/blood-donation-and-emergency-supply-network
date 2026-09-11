import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:blood_donation_network/core/secure_storage.dart';
import 'package:blood_donation_network/features/auth/auth_controller.dart';
import 'package:blood_donation_network/features/auth/login_screen.dart';
import 'package:blood_donation_network/main.dart';

/// In-memory SecureStorage so tests never touch the platform keychain.
class _FakeSecureStorage implements SecureStorage {
  final Map<String, String> _store = {};

  @override
  Future<void> saveSession({
    required String token,
    required String userId,
    required String role,
    String? organizationId,
  }) async {
    _store
      ..['token'] = token
      ..['userId'] = userId
      ..['role'] = role;
    if (organizationId != null) _store['orgId'] = organizationId;
  }

  @override
  Future<({String token, String userId, String role, String? organizationId})?>
      readSession() async {
    final token = _store['token'];
    if (token == null) return null;
    return (
      token: token,
      userId: _store['userId']!,
      role: _store['role']!,
      organizationId: _store['orgId'],
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

  test('AuthState defaults are coherent', () {
    const unknown = AuthState.unknown();
    const out = AuthState.signedOut();

    expect(unknown.status, AuthStatus.unknown);
    expect(unknown.isAuthenticated, isFalse);
    expect(out.status, AuthStatus.unauthenticated);
    expect(out.isAuthenticated, isFalse);
  });
}
