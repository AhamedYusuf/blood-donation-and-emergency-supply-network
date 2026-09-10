import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../theme/tokens.dart';
import 'app_card.dart';
import 'skeleton.dart';

/// Tracked-caps section label, e.g. UPCOMING / HISTORY.
class SectionLabel extends StatelessWidget {
  const SectionLabel(this.text, {super.key, this.trailing});
  final String text;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.xxs, 0, AppSpacing.xxs, AppSpacing.xs),
      child: Row(
        children: [
          Text(text.toUpperCase(), style: AppText.label),
          const Spacer(),
          ?trailing,
        ],
      ),
    );
  }
}

/// Designed empty state — a soft icon badge and two lines, never a bare
/// sentence.
class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.icon, required this.title, required this.message, this.action});
  final IconData icon;
  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 56,
              height: 56,
              decoration: const BoxDecoration(color: AppColors.surfaceSunken, shape: BoxShape.circle),
              child: Icon(icon, size: 26, color: AppColors.inkFaint),
            ),
            const SizedBox(height: AppSpacing.md),
            Text(title, style: AppText.headline),
            const SizedBox(height: AppSpacing.xxs),
            Text(message, style: AppText.bodySmall, textAlign: TextAlign.center),
            if (action != null) ...[const SizedBox(height: AppSpacing.md), action!],
          ],
        ),
      ),
    );
  }
}

class ErrorState extends StatelessWidget {
  const ErrorState({super.key, required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off_outlined, size: 26, color: AppColors.inkFaint),
            const SizedBox(height: AppSpacing.sm),
            Text(message, style: AppText.bodySmall, textAlign: TextAlign.center),
            const SizedBox(height: AppSpacing.md),
            OutlinedButton(
              onPressed: onRetry,
              style: OutlinedButton.styleFrom(minimumSize: const Size(120, 44)),
              child: const Text('Try again'),
            ),
          ],
        ),
      ),
    );
  }
}

/// Loading placeholder for the appointments list — one hero-shaped block
/// plus a few row-shaped blocks, matching the real layout so nothing jumps.
class AppointmentsSkeleton extends StatelessWidget {
  const AppointmentsSkeleton({super.key});

  @override
  Widget build(BuildContext context) {
    return Skeleton(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.xs, AppSpacing.gutter, AppSpacing.gutter),
        children: [
          const SectionLabel('Next donation'),
          AppCard(
            elevated: true,
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: const [
                SkeletonBox(width: 130, height: 14),
                SizedBox(height: 14),
                SkeletonBox(width: 180, height: 22),
                SizedBox(height: 10),
                SkeletonBox(width: 90, height: 12),
                SizedBox(height: 20),
                SkeletonBox(height: 44, radius: 8),
              ],
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          const SectionLabel('History'),
          for (var i = 0; i < 3; i++)
            Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.xs),
              child: AppCard(
                child: Row(
                  children: const [
                    SkeletonBox(width: 40, height: 40, radius: 8),
                    SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          SkeletonBox(width: 120, height: 13),
                          SizedBox(height: 8),
                          SkeletonBox(width: 70, height: 11),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}
