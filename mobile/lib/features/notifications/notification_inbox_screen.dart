import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/date_format.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/skeleton.dart';
import '../../widgets/states.dart';
import 'notification.dart';
import 'notification_inbox_repository.dart';

class NotificationInboxScreen extends ConsumerWidget {
  const NotificationInboxScreen({super.key});

  Future<void> _refresh(WidgetRef ref) async {
    ref.invalidate(notificationInboxProvider);
    await ref.read(notificationInboxProvider.future);
  }

  Future<void> _open(BuildContext context, WidgetRef ref, AppNotification n) async {
    if (n.isUnread) {
      try {
        await ref.read(notificationInboxRepositoryProvider).markAsRead(n.id);
        ref.invalidate(notificationInboxProvider);
        ref.invalidate(unreadNotificationCountProvider);
      } on ApiException {
        // Marking as read is best-effort — still let the donor navigate.
      }
    }
    if (n.appointmentId != null && context.mounted) {
      context.go('/appointments');
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(notificationInboxProvider);

    return Scaffold(
      appBar: AppBar(title: Text('Notifications', style: AppText.title)),
      body: async.when(
        loading: () => const _InboxSkeleton(),
        error: (err, _) => ErrorState(
          message: err is ApiException ? err.message : 'We couldn’t load your notifications.',
          onRetry: () => _refresh(ref),
        ),
        data: (notifications) {
          if (notifications.isEmpty) {
            return RefreshIndicator(
              color: AppColors.primary,
              onRefresh: () => _refresh(ref),
              child: LayoutBuilder(
                builder: (context, c) => SingleChildScrollView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  child: ConstrainedBox(
                    constraints: BoxConstraints(minHeight: c.maxHeight),
                    child: const EmptyState(
                      icon: Icons.notifications_none,
                      title: 'No notifications yet',
                      message: "You'll see donation matches and reminders here.",
                    ),
                  ),
                ),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () => _refresh(ref),
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(
                  AppSpacing.gutter, AppSpacing.xs, AppSpacing.gutter, AppSpacing.gutter),
              itemCount: notifications.length,
              separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.xs),
              itemBuilder: (_, i) {
                final n = notifications[i];
                return _NotificationRow(
                  notification: n,
                  onTap: () => _open(context, ref, n),
                );
              },
            ),
          );
        },
      ),
    );
  }
}

class _NotificationRow extends StatelessWidget {
  const _NotificationRow({required this.notification, required this.onTap});
  final AppNotification notification;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final n = notification;
    return AppCard(
      onTap: onTap,
      accent: n.isUnread ? AppColors.primary : null,
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  n.title,
                  style: n.isUnread
                      ? AppText.bodyStrong
                      : AppText.body.copyWith(color: AppColors.inkMuted),
                ),
                const SizedBox(height: 4),
                Text(
                  n.body,
                  style: AppText.bodySmall.copyWith(color: AppColors.inkMuted),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: AppSpacing.xxs),
                Text(relativeTime(n.createdAt), style: AppText.caption),
              ],
            ),
          ),
          if (n.isUnread) ...[
            const SizedBox(width: AppSpacing.xs),
            Container(
              width: 8,
              height: 8,
              margin: const EdgeInsets.only(top: 4),
              decoration: const BoxDecoration(color: AppColors.primary, shape: BoxShape.circle),
            ),
          ],
        ],
      ),
    );
  }
}

class _InboxSkeleton extends StatelessWidget {
  const _InboxSkeleton();

  @override
  Widget build(BuildContext context) {
    return Skeleton(
      child: ListView(
        padding: const EdgeInsets.fromLTRB(
            AppSpacing.gutter, AppSpacing.xs, AppSpacing.gutter, AppSpacing.gutter),
        children: [
          for (var i = 0; i < 4; i++)
            Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.xs),
              child: AppCard(
                padding: const EdgeInsets.all(AppSpacing.md),
                child: const Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    SkeletonBox(width: 160, height: 14),
                    SizedBox(height: 8),
                    SkeletonBox(width: 220, height: 12),
                    SizedBox(height: 8),
                    SkeletonBox(width: 60, height: 10),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}
