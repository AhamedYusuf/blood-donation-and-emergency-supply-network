import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/appointments/book_appointment_screen.dart';
import '../features/appointments/my_appointments_screen.dart';
import '../features/auth/auth_controller.dart';
import '../features/auth/login_screen.dart';
import '../features/auth/register_screen.dart';
import '../features/home/home_screen.dart';
import '../features/profile/profile_screen.dart';
import '../features/donor_profile/screens/donor_registration_screen.dart';
import '../features/donor_profile/screens/donor_profile_screen.dart';
import '../features/donor_profile/screens/eligibility_screen.dart';
import '../features/blood_requests/blood_request_details_screen.dart';
import '../features/blood_requests/blood_requests_screen.dart';
import '../features/blood_requests/coordinator_workflow_screen.dart';
import '../features/blood_requests/create_blood_request_screen.dart';
import 'mobile_shell.dart';

final _rootNavigatorKey = GlobalKey<NavigatorState>();

/// App router. Redirects between the login and home routes based on the
/// auth state, and shows a splash while the session is being restored.
final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    navigatorKey: _rootNavigatorKey,
    initialLocation: '/',

    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      final status = auth.status;

      final loggingIn =
          state.matchedLocation == '/login';

      final registering =
          state.matchedLocation == '/register';

      final donorRegistration =
          state.matchedLocation == '/donor-registration';

      final workflowRoute = state.matchedLocation
          .startsWith('/blood-requests/') &&
          state.matchedLocation.endsWith('/workflow');

      switch (status) {
        case AuthStatus.unknown:
          return '/splash';

        case AuthStatus.unauthenticated:
          return loggingIn || registering
              ? null
              : '/login';

        case AuthStatus.authenticated:
          final needsDonorRegistration =
              auth.role == 'donor' &&
              auth.donorProfileId == null;

          if (loggingIn ||
              registering ||
              state.matchedLocation == '/splash') {
            return needsDonorRegistration
                ? '/donor-registration'
                : '/';
          }

          if (needsDonorRegistration &&
              !donorRegistration) {
            return '/donor-registration';
          }

          // Coordinator Workflow is internal operational information.
          // Only staff and admins may access this screen.
          if (workflowRoute &&
              auth.role != 'staff' &&
              auth.role != 'admin') {
            return '/blood-requests';
          }

          return null;
      }
    },

    refreshListenable: _AuthRefresh(ref),

    routes: [
      StatefulShellRoute.indexedStack(
        builder: (
          context,
          state,
          shell,
        ) =>
            MobileShell(
          navigationShell: shell,
        ),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/',
                builder: (_, _) =>
                    const HomeScreen(),
              ),
            ],
          ),

          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/appointments',
                builder: (_, _) =>
                    const MyAppointmentsScreen(),
              ),
            ],
          ),

          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/profile',
                builder: (_, _) =>
                    const ProfileScreen(),
              ),
            ],
          ),

          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/blood-requests',
                builder: (_, _) =>
                    const BloodRequestsScreen(),
              ),
            ],
          ),
        ],
      ),

      GoRoute(
        path: '/appointments/book',
        parentNavigatorKey:
            _rootNavigatorKey,
        builder: (_, _) =>
            const BookAppointmentScreen(),
      ),

      GoRoute(
        path: '/blood-requests/new',
        parentNavigatorKey:
            _rootNavigatorKey,
        builder: (_, _) =>
            const CreateBloodRequestScreen(),
      ),

      GoRoute(
        path: '/blood-requests/:id',
        parentNavigatorKey:
            _rootNavigatorKey,
        builder: (_, state) =>
            BloodRequestDetailsScreen(
          requestId:
              state.pathParameters['id']!,
        ),
      ),

      GoRoute(
        path:
            '/blood-requests/:id/workflow',
        parentNavigatorKey:
            _rootNavigatorKey,
        builder: (_, state) =>
            CoordinatorWorkflowScreen(
          requestId:
              state.pathParameters['id']!,
        ),
      ),

      GoRoute(
        path: '/login',
        builder: (_, _) =>
            const LoginScreen(),
      ),

      GoRoute(
        path: '/register',
        builder: (_, _) =>
            const RegisterScreen(),
      ),

      GoRoute(
        path: '/donor-registration',
        builder: (_, _) =>
            const DonorRegistrationScreen(),
      ),

      GoRoute(
        path: '/donor-profile',
        builder: (_, _) =>
            const DonorProfileScreen(),
      ),

      GoRoute(
        path: '/eligibility',
        builder: (_, _) =>
            const EligibilityScreen(),
      ),

      GoRoute(
        path: '/splash',
        builder: (_, _) =>
            const Scaffold(
          body: Center(
            child:
                CircularProgressIndicator(),
          ),
        ),
      ),
    ],
  );
});

/// Bridges Riverpod auth state to GoRouter refresh.
class _AuthRefresh extends ChangeNotifier {
  _AuthRefresh(Ref ref) {
    ref.listen(
      authControllerProvider,
      (_, _) => notifyListeners(),
    );
  }
}