import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/auth/auth_controller.dart';
import '../features/auth/login_screen.dart';
import '../features/home/home_screen.dart';

/// App router. Redirects between the login and home routes based on the
/// auth state, and shows a splash while the session is being restored.
final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
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
      GoRoute(path: '/', builder: (_, _) => const HomeScreen()),
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
