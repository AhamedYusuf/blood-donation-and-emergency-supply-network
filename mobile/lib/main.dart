import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/router.dart';
import 'theme/app_theme.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Reads android/app/google-services.json (processed at build time by the
  // Google Services Gradle plugin) — no explicit FirebaseOptions needed on
  // Android. If Firebase hasn't been set up on this platform yet, don't let
  // that stop the app from launching; StubFcmTokenSource covers that case.
  try {
    await Firebase.initializeApp();
  } catch (_) {
    // No Firebase config for this platform/build — push registration will
    // just no-op (see fcm_token_source.dart).
  }

  runApp(const ProviderScope(child: BloodDonationApp()));
}

class BloodDonationApp extends ConsumerWidget {
  const BloodDonationApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);

    return MaterialApp.router(
      title: 'Blood Donation Network',
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      routerConfig: router,
    );
  }
}
