import 'dart:async';

import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app/router.dart';
import 'features/auth/auth_controller.dart';
import 'features/notifications/blood_request_notification_router.dart';
import 'features/notifications/fcm_message_handler.dart';
import 'theme/app_theme.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Reads android/app/google-services.json (processed at build time by the
  // Google Services Gradle plugin) — no explicit FirebaseOptions needed on
  // Android. If Firebase hasn't been set up on this platform yet, don't let
  // that stop the app from launching; StubFcmTokenSource covers that case.
  try {
    await Firebase.initializeApp();
    FirebaseMessaging.onBackgroundMessage(
      firebaseMessagingBackgroundHandler,
    );
  } catch (error) {
    // No Firebase config for this platform/build — push registration will
    // just no-op (see fcm_token_source.dart).
    debugPrint('Firebase initialization skipped: $error');
  }

  runApp(const ProviderScope(child: BloodDonationApp()));
}

final appScaffoldMessengerKey = GlobalKey<ScaffoldMessengerState>();

class BloodDonationApp extends ConsumerStatefulWidget {
  const BloodDonationApp({super.key});

  @override
  ConsumerState<BloodDonationApp> createState() => _BloodDonationAppState();
}

class _BloodDonationAppState extends ConsumerState<BloodDonationApp> {
  final _notificationRouter = BloodRequestNotificationRouter();
  final _subscriptions = <StreamSubscription<RemoteMessage>>[];
  late final ProviderSubscription<AuthState> _authSubscription;

  @override
  void initState() {
    super.initState();
    _authSubscription = ref.listenManual<AuthState>(
      authControllerProvider,
      (_, auth) {
        if (auth.isAuthenticated) _navigateToPendingNotification();
      },
    );
    unawaited(_initializeMessaging());
  }

  Future<void> _initializeMessaging() async {
    try {
      if (Firebase.apps.isEmpty) return;

      final messaging = FirebaseMessaging.instance;
      _subscriptions.add(
        FirebaseMessaging.onMessage.listen(
          _showForegroundNotification,
          onError: (Object error) {
            debugPrint('Foreground FCM listener failed: $error');
          },
        ),
      );
      _subscriptions.add(
        FirebaseMessaging.onMessageOpenedApp.listen(
          _handleNotificationTap,
          onError: (Object error) {
            debugPrint('FCM notification tap listener failed: $error');
          },
        ),
      );

      final initialMessage = await messaging.getInitialMessage();
      if (initialMessage != null) _handleNotificationTap(initialMessage);
    } catch (error) {
      debugPrint('Firebase Messaging is unavailable: $error');
    }
  }

  void _showForegroundNotification(RemoteMessage message) {
    final route = BloodRequestNotificationRouter.routeFromData(message.data);
    if (route == null) {
      debugPrint('Ignoring unrelated or invalid foreground FCM message.');
      return;
    }

    final notification = message.notification;
    appScaffoldMessengerKey.currentState
      ?..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(
            '${notification?.title ?? 'New Blood Request Near You'}\n'
            '${notification?.body ?? 'A new blood request is available near you.'}',
          ),
          duration: const Duration(seconds: 8),
          action: SnackBarAction(
            label: 'View',
            onPressed: () => _handleNotificationTap(message),
          ),
        ),
      );
  }

  void _handleNotificationTap(RemoteMessage message) {
    final route = _notificationRouter.handleTap(
      message.data,
      isAuthenticated: ref.read(authControllerProvider).isAuthenticated,
      messageId: message.messageId,
    );

    if (route == null) {
      if (BloodRequestNotificationRouter.routeFromData(message.data) == null) {
        debugPrint('Ignoring unrelated or invalid FCM notification tap.');
      }
      return;
    }

    ref.read(routerProvider).go(route);
  }

  void _navigateToPendingNotification() {
    if (!_notificationRouter.hasPendingRoute) return;

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted ||
          !ref.read(authControllerProvider).isAuthenticated) {
        return;
      }

      final route = _notificationRouter.takePendingRoute();
      if (route != null) ref.read(routerProvider).go(route);
    });
  }

  @override
  void dispose() {
    _authSubscription.close();
    for (final subscription in _subscriptions) {
      unawaited(subscription.cancel());
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final router = ref.watch(routerProvider);

    return MaterialApp.router(
      title: 'PulsePoint',
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      scaffoldMessengerKey: appScaffoldMessengerKey,
      routerConfig: router,
    );
  }
}
