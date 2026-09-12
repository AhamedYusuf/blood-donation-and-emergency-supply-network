import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/appointments/book_appointment_screen.dart';
import '../features/appointments/my_appointments_screen.dart';
import '../features/auth/auth_controller.dart';
import '../features/auth/login_screen.dart';
import '../features/home/home_screen.dart';
import '../features/profile/profile_screen.dart';
import 'mobile_shell.dart';

final _rootNavigatorKey = GlobalKey<NavigatorState>();

/// App router. Redirects between the login and home routes based on the
/// auth state, and shows a splash while the session is being restored.
/// The three tab destinations (Home / Donations / Profile) live behind a
/// persistent bottom nav via [StatefulShellRoute.indexedStack]; booking a
/// donation pushes full-screen on the root navigator, above the tab bar.
final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    navigatorKey: _rootNavigatorKey,
    initialLocation: '/',
    redirect: (context, state) {
      final status = ref.read(authControllerProvider).status;
      final loggingIn = state.matchedLocation == '/login';

      switch (status) {
        case AuthStatus.unknown:
          return '/splash';
        case AuthStatus.unauthenticated:
          return loggingIn ? null : '/login';
        case AuthStatus.authenticated:
          return (loggingIn || state.matchedLocation == '/splash') ? '/' : null;
      }
    },
    refreshListenable: _AuthRefresh(ref),
    routes: [
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => MobileShell(navigationShell: shell),
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(path: '/', builder: (_, _) => const HomeScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/appointments', builder: (_, _) => const MyAppointmentsScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/profile', builder: (_, _) => const ProfileScreen()),
          ]),
        ],
      ),
      GoRoute(
        path: '/appointments/book',
        parentNavigatorKey: _rootNavigatorKey,
        builder: (_, _) => const BookAppointmentScreen(),
      ),
      GoRoute(path: '/login', builder: (_, _) => const LoginScreen()),
      GoRoute(
        path: '/splash',
        builder: (_, _) => const Scaffold(
          body: Center(child: CircularProgressIndicator()),
        ),
      ),
    ],
  );
});

/// Bridges Riverpod's auth state to GoRouter's Listenable-based refresh.
class _AuthRefresh extends ChangeNotifier {
  _AuthRefresh(Ref ref) {
    ref.listen(authControllerProvider, (_, _) => notifyListeners());
  }
}
