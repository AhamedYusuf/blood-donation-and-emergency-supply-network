import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Wraps flutter_secure_storage (Keychain on iOS, EncryptedSharedPreferences
/// on Android) for the auth token and its associated claims.
class SecureStorage {
  SecureStorage([FlutterSecureStorage? storage])
      : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
            );

  final FlutterSecureStorage _storage;

  static const _kToken = 'auth_token';
  static const _kUserId = 'auth_user_id';
  static const _kRole = 'auth_role';
  static const _kOrgId = 'auth_org_id';
  static const _kEmail = 'auth_email';
  static const _kFullName = 'auth_full_name';

  Future<void> saveSession({
    required String token,
    required String userId,
    required String role,
    String? organizationId,
    String? email,
    String? fullName,
  }) async {
    await _storage.write(key: _kToken, value: token);
    await _storage.write(key: _kUserId, value: userId);
    await _storage.write(key: _kRole, value: role);
    if (organizationId != null) {
      await _storage.write(key: _kOrgId, value: organizationId);
    } else {
      await _storage.delete(key: _kOrgId);
    }
    if (email != null) await _storage.write(key: _kEmail, value: email);
    if (fullName != null) {
      await _storage.write(key: _kFullName, value: fullName);
    }
  }

  Future<
      ({
        String token,
        String userId,
        String role,
        String? organizationId,
        String? email,
        String? fullName,
      })?> readSession() async {
    final token = await _storage.read(key: _kToken);
    final userId = await _storage.read(key: _kUserId);
    final role = await _storage.read(key: _kRole);
    if (token == null || userId == null || role == null) return null;
    return (
      token: token,
      userId: userId,
      role: role,
      organizationId: await _storage.read(key: _kOrgId),
      email: await _storage.read(key: _kEmail),
      fullName: await _storage.read(key: _kFullName),
    );
  }

  Future<void> clear() async {
    await _storage.deleteAll();
  }
}

final secureStorageProvider = Provider<SecureStorage>((ref) => SecureStorage());
