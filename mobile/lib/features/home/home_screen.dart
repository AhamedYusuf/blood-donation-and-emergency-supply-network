import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/brand_mark.dart';
import '../../widgets/states.dart';
import '../auth/auth_controller.dart';

/// Donor home. The action list mirrors the web console's nav rail — the
/// screens teammates own appear as "soon" so the product reads as a whole.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(
        titleSpacing: AppSpacing.gutter,
        title: Row(
          children: [
            const BrandMark(size: 22, color: AppColors.ink),
            const SizedBox(width: AppSpacing.xs),
            Text('Blood Donation Network', style: AppText.headline),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Sign out',
            icon: const Icon(Icons.logout, size: 20),
            onPressed: () => ref.read(authControllerProvider.notifier).logout(),
          ),
          const SizedBox(width: AppSpacing.xxs),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.md, AppSpacing.gutter, AppSpacing.xl),
        children: [
          Text('Welcome back', style: AppText.display),
          const SizedBox(height: AppSpacing.xxs),
          Text('Thank you for giving blood. Here’s everything in one place.', style: AppText.body),
          const SizedBox(height: AppSpacing.lg),

          const SectionLabel('Appointments'),
          _ActionTile(
            icon: Icons.event_available_outlined,
            title: 'My donations',
            subtitle: 'View, book and manage your appointments',
            onTap: () => context.push('/appointments'),
          ),
          const SizedBox(height: AppSpacing.xs),
          _ActionTile(
            icon: Icons.add_circle_outline,
            title: 'Book a donation',
            subtitle: 'Pick a blood bank and a time',
            onTap: () => context.push('/appointments/book'),
          ),

          const SizedBox(height: AppSpacing.lg),
          const SectionLabel('Coming soon'),
          const _ActionTile(
            icon: Icons.notifications_none,
            title: 'Urgent alerts',
            subtitle: 'Requests matched to your blood type',
          ),
          const SizedBox(height: AppSpacing.xs),
          const _ActionTile(
            icon: Icons.verified_outlined,
            title: 'My eligibility',
            subtitle: 'When you can next donate',
          ),
          const SizedBox(height: AppSpacing.xs),
          const _ActionTile(
            icon: Icons.map_outlined,
            title: 'Nearby blood banks',
            subtitle: 'Find a place to donate',
          ),
        ],
      ),
    );
  }
}

class _ActionTile extends StatelessWidget {
  const _ActionTile({required this.icon, required this.title, required this.subtitle, this.onTap});
  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;
    return Opacity(
      opacity: enabled ? 1 : 0.55,
      child: AppCard(
        onTap: onTap,
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Row(
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: enabled ? AppColors.primarySubtle : AppColors.surfaceSunken,
                borderRadius: BorderRadius.circular(AppRadii.sm),
              ),
              child: Icon(icon, size: 20, color: enabled ? AppColors.primary : AppColors.inkMuted),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: AppText.bodyStrong),
                  const SizedBox(height: 1),
                  Text(subtitle, style: AppText.caption),
                ],
              ),
            ),
            if (enabled) const Icon(Icons.chevron_right, color: AppColors.inkFaint),
          ],
        ),
      ),
    );
  }
}
