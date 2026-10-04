import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../widgets/app_bottom_nav.dart';

/// Hosts the four persistent donor tabs (Home / Donations / Requests / Profile) behind one
/// bottom nav bar, each keeping its own navigation stack and scroll
/// position via [StatefulShellRoute.indexedStack] — switching tabs never
/// rebuilds the others from scratch.
class MobileShell extends StatelessWidget {
  const MobileShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const _tabToBranchIndex = [0, 1, 3, 2];

  static const _tabs = [
    NavTab(icon: Icons.home_outlined, activeIcon: Icons.home_rounded, label: 'Home'),
    NavTab(icon: Icons.event_available_outlined, activeIcon: Icons.event_available_rounded, label: 'Donations'),
    NavTab(icon: Icons.bloodtype_outlined, activeIcon: Icons.bloodtype_rounded, label: 'Requests'),
    NavTab(icon: Icons.person_outline, activeIcon: Icons.person_rounded, label: 'Profile'),
  ];

  static int selectedIndexForLocation(
    String location, {
    required int fallbackIndex,
  }) {
    final path = Uri.parse(location).path;

    if (path == '/' || path == '/home') return 0;
    if (path == '/donations' ||
        path == '/appointments' ||
        path.startsWith('/appointments/')) {
      return 1;
    }
    if (path == '/blood-requests' ||
        path.startsWith('/blood-requests/')) {
      return 2;
    }
    if (path == '/profile' || path.startsWith('/profile/')) return 3;

    return fallbackIndex;
  }

  @override
  Widget build(BuildContext context) {
    final router = GoRouter.of(context);

    return AnimatedBuilder(
      animation: router.routerDelegate,
      builder: (context, _) {
        final selectedIndex = selectedIndexForLocation(
          router.state.uri.path,
          fallbackIndex: navigationShell.currentIndex,
        );

        return Scaffold(
          body: navigationShell,
          bottomNavigationBar: AppBottomNav(
            tabs: _tabs,
            currentIndex: selectedIndex,
            onTap: (index) {
              final branchIndex = _tabToBranchIndex[index];
              navigationShell.goBranch(
                branchIndex,
                initialLocation:
                    branchIndex == navigationShell.currentIndex,
              );
            },
          ),
        );
      },
    );
  }
}
