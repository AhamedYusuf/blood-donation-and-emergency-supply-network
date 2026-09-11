import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/secure_storage.dart';
import '../notifications/devices_repository.dart';
import 'auth_repository.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

class AuthState {
  const AuthState({
    required this.status,
    this.token,
    this.userId,
    this.role,
    this.organizationId,
    this.email,
    this.fullName,
  });

  const AuthState.unknown() : this(status: AuthStatus.unknown);
  const AuthState.signedOut() : this(status: AuthStatus.unauthenticated);

  final AuthStatus status;
  final String? token;
  final String? userId;
  final String? role;
  final String? organizationId;
  final String? email;
  final String? fullName;

  bool get isAuthenticated => status == AuthStatus.authenticated;

  /// First-name-only greeting, e.g. "Amara" from "Amara Silva". Falls back
  /// to the email's local part, then to null (never a fake placeholder).
  String? get firstName {
    final name = fullName?.trim();
    if (name != null && name.isNotEmpty) return name.split(RegExp(r'\s+')).first;
    final at = email?.indexOf('@') ?? -1;
    if (email != null && at > 0) return email!.substring(0, at);
    return null;
  }

  /// One or two initials for the avatar badge, derived the same way.
  String get initials {
    final name = fullName?.trim();
    if (name != null && name.isNotEmpty) {
      final parts = name.split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
      if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
      return parts.first.substring(0, 1).toUpperCase();
    }
    if (email != null && email!.isNotEmpty) return email![0].toUpperCase();
    return '?';
  }
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
            email: session.email,
            fullName: session.fullName,
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
      email: result.email,
      fullName: result.fullName,
    );
    state = AuthState(
      status: AuthStatus.authenticated,
      token: result.token,
      userId: result.userId,
      role: result.role,
      organizationId: result.organizationId,
      email: result.email,
      fullName: result.fullName,
    );

    // Best-effort: tell the backend where to push this donor's alerts.
    // Never let a registration failure surface as a failed login.
    unawaited(_bestEffort(
        () => ref.read(devicesRepositoryProvider).registerCurrentDevice()));
  }

  Future<void> logout() async {
    final token = state.token;
    await _bestEffort(() => ref
        .read(devicesRepositoryProvider)
        .unregisterCurrentDevice(sessionToken: token));

    await _storage.clear();
    state = const AuthState.signedOut();
  }

  static Future<void> _bestEffort(Future<void> Function() action) async {
    try {
      await action();
    } catch (_) {
      // non-fatal — retried on the next login
    }
  }
}

final authControllerProvider =
    NotifierProvider<AuthController, AuthState>(AuthController.new);

/// The current bearer token, or null. Split out so feature code (and tests)
/// can depend on just the token without the whole auth state.
final authTokenProvider = Provider<String?>(
  (ref) => ref.watch(authControllerProvider).token,
);

/// The current user's id, or null.
final currentUserIdProvider = Provider<String?>(
  (ref) => ref.watch(authControllerProvider).userId,
);
