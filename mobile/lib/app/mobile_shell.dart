import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/auth/auth_controller.dart';
import '../widgets/app_bottom_nav.dart';

/// Hosts the persistent tabs behind one bottom nav bar, each keeping its own
/// navigation stack and scroll position via [StatefulShellRoute.indexedStack].
/// Donors get Home / Donations / Requests / Profile; staff and admins have no
/// donations of their own, so they get Home / Requests / Profile.
class MobileShell extends ConsumerWidget {
  const MobileShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const _donorTabToBranchIndex = [0, 1, 3, 2];
  static const _staffTabToBranchIndex = [0, 3, 2];

  static const _home = NavTab(icon: Icons.home_outlined, activeIcon: Icons.home_rounded, label: 'Home');
  static const _donations = NavTab(icon: Icons.event_available_outlined, activeIcon: Icons.event_available_rounded, label: 'Donations');
  static const _requests = NavTab(icon: Icons.bloodtype_outlined, activeIcon: Icons.bloodtype_rounded, label: 'Requests');
  static const _profile = NavTab(icon: Icons.person_outline, activeIcon: Icons.person_rounded, label: 'Profile');

  static const _donorTabs = [_home, _donations, _requests, _profile];
  static const _staffTabs = [_home, _requests, _profile];

  static int selectedIndexForLocation(
    String location, {
    required int fallbackIndex,
    bool isStaff = false,
  }) {
    final path = Uri.parse(location).path;

    if (path == '/' || path == '/home') return 0;
    if (!isStaff &&
        (path == '/donations' ||
            path == '/appointments' ||
            path.startsWith('/appointments/'))) {
      return 1;
    }
    if (path == '/blood-requests' ||
        path.startsWith('/blood-requests/')) {
      return isStaff ? 1 : 2;
    }
    if (path == '/profile' || path.startsWith('/profile/')) {
      return isStaff ? 2 : 3;
    }

    return fallbackIndex;
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = GoRouter.of(context);
    final role = ref.watch(authControllerProvider).role;
    final isStaff = role == 'staff' || role == 'admin';
    final tabs = isStaff ? _staffTabs : _donorTabs;
    final tabToBranch = isStaff ? _staffTabToBranchIndex : _donorTabToBranchIndex;
    final branchToTab = {for (var i = 0; i < tabToBranch.length; i++) tabToBranch[i]: i};

    return AnimatedBuilder(
      animation: router.routerDelegate,
      builder: (context, _) {
        final selectedIndex = selectedIndexForLocation(
          router.state.uri.path,
          fallbackIndex: branchToTab[navigationShell.currentIndex] ?? 0,
          isStaff: isStaff,
        );

        return Scaffold(
          body: navigationShell,
          bottomNavigationBar: AppBottomNav(
            tabs: tabs,
            currentIndex: selectedIndex,
            onTap: (index) {
              final branchIndex = tabToBranch[index];
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
