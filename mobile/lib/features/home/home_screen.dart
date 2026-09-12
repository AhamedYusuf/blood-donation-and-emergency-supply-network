import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/avatar.dart';
import '../../widgets/fade_slide_in.dart';
import '../../widgets/states.dart';
import '../appointments/appointment.dart';
import '../appointments/appointments_repository.dart';
import '../auth/auth_controller.dart';

/// Donor home. Plain iOS-style large title — no colour block, no
/// gradient — with the donor's own avatar as the way into Profile. The
/// action list mirrors the web console's nav rail; screens teammates own
/// appear as "soon" so the product reads as a whole.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  static String _greeting() {
    final h = DateTime.now().hour;
    if (h < 12) return 'Good morning';
    if (h < 17) return 'Good afternoon';
    return 'Good evening';
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final appointments = ref.watch(myAppointmentsProvider);

    return Scaffold(
      backgroundColor: AppColors.canvas,
      body: SafeArea(
        bottom: false,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.xs, AppSpacing.gutter, AppSpacing.xl),
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(_greeting(), style: AppText.bodySmall),
                      Text(auth.firstName ?? 'Donor', style: AppText.largeTitle),
                    ],
                  ),
                ),
                InkWell(
                  onTap: () => context.go('/profile'),
                  customBorder: const CircleBorder(),
                  child: InitialsAvatar(initials: auth.initials, size: 40),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.lg),
            _StatsRow(appointments: appointments),
            const SizedBox(height: AppSpacing.xl),
            const SectionLabel('Appointments'),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  FadeSlideIn(
                    index: 0,
                    child: _ActionRow(
                      icon: Icons.event_available_outlined,
                      iconColor: AppColors.primary,
                      iconBg: AppColors.primarySubtle,
                      title: 'My donations',
                      subtitle: 'View, book and manage your appointments',
                      onTap: () => context.go('/appointments'),
                    ),
                  ),
                  const Divider(height: 1, indent: AppSpacing.md + 40 + AppSpacing.sm),
                  FadeSlideIn(
                    index: 1,
                    child: _ActionRow(
                      icon: Icons.add_circle_outline,
                      iconColor: AppColors.primary,
                      iconBg: AppColors.primarySubtle,
                      title: 'Book a donation',
                      subtitle: 'Pick a blood bank and a time',
                      onTap: () => context.push('/appointments/book'),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.xl),
            const SectionLabel('Coming soon'),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  FadeSlideIn(
                    index: 2,
                    child: const _ActionRow(
                      icon: Icons.notifications_none,
                      iconColor: AppColors.inkMuted,
                      iconBg: AppColors.surfaceSunken,
                      title: 'Urgent alerts',
                      subtitle: 'Requests matched to your blood type',
                    ),
                  ),
                  const Divider(height: 1, indent: AppSpacing.md + 40 + AppSpacing.sm),
                  FadeSlideIn(
                    index: 3,
                    child: const _ActionRow(
                      icon: Icons.verified_outlined,
                      iconColor: AppColors.inkMuted,
                      iconBg: AppColors.surfaceSunken,
                      title: 'My eligibility',
                      subtitle: 'When you can next donate',
                    ),
                  ),
                  const Divider(height: 1, indent: AppSpacing.md + 40 + AppSpacing.sm),
                  FadeSlideIn(
                    index: 4,
                    child: const _ActionRow(
                      icon: Icons.map_outlined,
                      iconColor: AppColors.inkMuted,
                      iconBg: AppColors.surfaceSunken,
                      title: 'Nearby blood banks',
                      subtitle: 'Find a place to donate',
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ── stats row ────────────────────────────────────────────────────────────

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.appointments});
  final AsyncValue<List<Appointment>> appointments;

  @override
  Widget build(BuildContext context) {
    final upcoming = appointments.valueOrNull?.where((a) => a.isUpcoming).length;
    final completed = appointments.valueOrNull?.where((a) => a.status == 'completed').length;

    return Row(
      children: [
        Expanded(
          child: _StatCard(
            icon: Icons.event_outlined,
            iconColor: AppColors.primary,
            label: 'Upcoming',
            value: upcoming,
            loading: appointments.isLoading,
          ),
        ),
        const SizedBox(width: AppSpacing.sm),
        Expanded(
          child: _StatCard(
            icon: Icons.water_drop_outlined,
            iconColor: AppColors.success,
            label: 'Completed',
            value: completed,
            loading: appointments.isLoading,
          ),
        ),
      ],
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.icon, required this.iconColor, required this.label, required this.value, required this.loading});
  final IconData icon;
  final Color iconColor;
  final String label;
  final int? value;
  final bool loading;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Row(
        children: [
          Icon(icon, size: 18, color: iconColor),
          const SizedBox(width: AppSpacing.xs),
          Text(
            loading || value == null ? '—' : '$value',
            style: AppText.title.copyWith(fontSize: 22),
          ),
          const SizedBox(width: 6),
          Expanded(child: Text(label, style: AppText.bodySmall, overflow: TextOverflow.ellipsis)),
        ],
      ),
    );
  }
}

// ── grouped-list action row ─────────────────────────────────────────────

class _ActionRow extends StatelessWidget {
  const _ActionRow({
    required this.icon,
    required this.iconColor,
    required this.iconBg,
    required this.title,
    required this.subtitle,
    this.onTap,
  });
  final IconData icon;
  final Color iconColor;
  final Color iconBg;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        child: Opacity(
          opacity: enabled ? 1 : 0.5,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.sm),
            child: Row(
              children: [
                Container(
                  width: 32,
                  height: 32,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(color: iconBg, borderRadius: BorderRadius.circular(AppRadii.sm - 2)),
                  child: Icon(icon, size: 17, color: iconColor),
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
                if (enabled) const Icon(Icons.chevron_right, size: 20, color: AppColors.inkFaint),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
