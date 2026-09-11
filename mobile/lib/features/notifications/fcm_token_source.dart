import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Where the FCM registration token comes from. Abstracted so the rest of
/// the app (and its tests) don't depend on `firebase_messaging`.
///
/// The real implementation will wrap `FirebaseMessaging.instance` once the
/// Firebase project + `google-services.json` / `GoogleService-Info.plist`
/// are in place. Until then [StubFcmTokenSource] returns null and device
/// registration is simply skipped.
abstract class FcmTokenSource {
  Future<String?> getToken();

  /// "android" | "ios" | "web"
  String get platform;
}

class StubFcmTokenSource implements FcmTokenSource {
  const StubFcmTokenSource();

  @override
  Future<String?> getToken() async => null;

  @override
  String get platform => 'android';
}

final fcmTokenSourceProvider =
    Provider<FcmTokenSource>((ref) => const StubFcmTokenSource());
