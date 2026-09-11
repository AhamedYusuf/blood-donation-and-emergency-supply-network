import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../widgets/app_bottom_nav.dart';

/// Hosts the three persistent tabs (Home / Donations / Profile) behind one
/// bottom nav bar, each keeping its own navigation stack and scroll
/// position via [StatefulShellRoute.indexedStack] — switching tabs never
/// rebuilds the others from scratch.
class MobileShell extends StatelessWidget {
  const MobileShell({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const _tabs = [
    NavTab(icon: Icons.home_outlined, activeIcon: Icons.home_rounded, label: 'Home'),
    NavTab(icon: Icons.event_available_outlined, activeIcon: Icons.event_available_rounded, label: 'Donations'),
    NavTab(icon: Icons.person_outline, activeIcon: Icons.person_rounded, label: 'Profile'),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: AppBottomNav(
        tabs: _tabs,
        currentIndex: navigationShell.currentIndex,
        onTap: (i) => navigationShell.goBranch(i, initialLocation: i == navigationShell.currentIndex),
      ),
    );
  }
}
