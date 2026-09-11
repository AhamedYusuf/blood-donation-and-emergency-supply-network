import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Where the FCM registration token comes from. Abstracted so the rest of
/// the app (and its tests) don't depend on `firebase_messaging` directly.
abstract class FcmTokenSource {
  Future<String?> getToken();

  /// "android" | "ios" | "web"
  String get platform;
}

/// Used before Firebase is configured for a platform (e.g. iOS, until its
/// `GoogleService-Info.plist` is added) — registration is just skipped.
class StubFcmTokenSource implements FcmTokenSource {
  const StubFcmTokenSource();

  @override
  Future<String?> getToken() async => null;

  @override
  String get platform => 'android';
}

/// Real implementation, backed by `firebase_messaging`. Requests
/// notification permission (required on Android 13+ and iOS) before asking
/// for a token; returns null rather than throwing if Firebase isn't
/// actually initialised or the user declines permission — the caller
/// (DevicesRepository) treats "no token" as a normal, silent no-op.
class FirebaseFcmTokenSource implements FcmTokenSource {
  const FirebaseFcmTokenSource();

  @override
  Future<String?> getToken() async {
    try {
      final messaging = FirebaseMessaging.instance;

      final settings = await messaging.requestPermission(
        alert: true,
        badge: true,
        sound: true,
      );

      final granted = settings.authorizationStatus == AuthorizationStatus.authorized ||
          settings.authorizationStatus == AuthorizationStatus.provisional;
      if (!granted) return null;

      return await messaging.getToken();
    } catch (_) {
      // Firebase not configured for this platform/build, or the platform
      // call failed — treat exactly like "no token yet".
      return null;
    }
  }

  @override
  String get platform => switch (defaultTargetPlatform) {
        TargetPlatform.iOS => 'ios',
        TargetPlatform.android => 'android',
        _ => 'web',
      };
}

final fcmTokenSourceProvider =
    Provider<FcmTokenSource>((ref) => const FirebaseFcmTokenSource());
