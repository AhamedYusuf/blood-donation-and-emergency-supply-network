import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';

/// Result of a successful login — mirrors the backend AuthResponse.
class AuthResult {
  AuthResult({
    required this.token,
    required this.userId,
    required this.email,
    required this.fullName,
    required this.role,
    this.organizationId,
  });

  final String token;
  final String userId;
  final String email;
  final String fullName;
  final String role;
  final String? organizationId;

  factory AuthResult.fromJson(Map<String, dynamic> json) => AuthResult(
        token: json['accessToken'] as String,
        userId: json['userId'] as String,
        email: json['email'] as String,
        fullName: json['fullName'] as String? ?? '',
        role: (json['role'] as String).toLowerCase(),
        organizationId: json['organizationId'] as String?,
      );
}

class AuthRepository {
  AuthRepository(this._api);

  final ApiClient _api;

  Future<AuthResult> login({
    required String email,
    required String password,
  }) async {
    final json = await _api.post(
      '/api/auth/login',
      body: {'email': email, 'password': password},
    );
    return AuthResult.fromJson(json as Map<String, dynamic>);
  }
}

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(ref.watch(apiClientProvider)),
);
