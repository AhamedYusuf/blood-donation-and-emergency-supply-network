import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/secure_storage.dart';
import 'auth_repository.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

class AuthState {
  const AuthState({
    required this.status,
    this.token,
    this.userId,
    this.role,
    this.organizationId,
  });

  const AuthState.unknown() : this(status: AuthStatus.unknown);
  const AuthState.signedOut() : this(status: AuthStatus.unauthenticated);

  final AuthStatus status;
  final String? token;
  final String? userId;
  final String? role;
  final String? organizationId;

  bool get isAuthenticated => status == AuthStatus.authenticated;
}

/// Owns the session: restores it from secure storage on startup, and
/// exposes login / logout. Screens watch this to react to auth changes;
/// the router redirects on it.
class AuthController extends Notifier<AuthState> {
  @override
  AuthState build() {
    // Kick off the restore, but start in `unknown` so the router can show
    // a splash instead of flashing the login screen.
    Future.microtask(_restore);
    return const AuthState.unknown();
  }

  SecureStorage get _storage => ref.read(secureStorageProvider);
  AuthRepository get _repo => ref.read(authRepositoryProvider);

  Future<void> _restore() async {
    final session = await _storage.readSession();
    state = session == null
        ? const AuthState.signedOut()
        : AuthState(
            status: AuthStatus.authenticated,
            token: session.token,
            userId: session.userId,
            role: session.role,
            organizationId: session.organizationId,
          );
  }

  /// Throws [ApiException] on failure; the caller shows the message.
  Future<void> login({required String email, required String password}) async {
    final result = await _repo.login(email: email, password: password);
    await _storage.saveSession(
      token: result.token,
      userId: result.userId,
      role: result.role,
      organizationId: result.organizationId,
    );
    state = AuthState(
      status: AuthStatus.authenticated,
      token: result.token,
      userId: result.userId,
      role: result.role,
      organizationId: result.organizationId,
    );
  }

  Future<void> logout() async {
    await _storage.clear();
    state = const AuthState.signedOut();
  }
}

final authControllerProvider =
    NotifierProvider<AuthController, AuthState>(AuthController.new);
