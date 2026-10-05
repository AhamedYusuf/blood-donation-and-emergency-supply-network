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
import '../blood_requests/blood_request_ui.dart';
import '../blood_requests/blood_requests_repository.dart';
import '../donor_profile/eligibility_text.dart';
import '../donor_profile/providers/donor_profile_provider.dart';
import '../notifications/notification_inbox_repository.dart';

/// Home tab. Donors see their donations and what they can do next; staff
/// and admins see their organization's blood requests. Every row on this
/// screen opens a working feature.
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
    final isDonor = (auth.role ?? 'donor') == 'donor';

    return Scaffold(
      backgroundColor: AppColors.canvas,
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () async {
            // Eligibility hangs off the donor profile, so refreshing the
            // profile also picks up an admin verifying the donor.
            ref.invalidate(myAppointmentsProvider);
            ref.invalidate(donorProfileProvider);
            ref.invalidate(unreadNotificationCountProvider);
            ref.invalidate(bloodRequestsProvider);
          },
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.gutter,
              AppSpacing.xs,
              AppSpacing.gutter,
              AppSpacing.xl,
            ),
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(_greeting(), style: AppText.bodySmall),
                        Text(
                          auth.firstName ?? (isDonor ? 'Donor' : 'Staff'),
                          style: AppText.largeTitle,
                        ),
                      ],
                    ),
                  ),
                  if (isDonor) ...[
                    const _NotificationBell(),
                    const SizedBox(width: AppSpacing.xs),
                  ],
                  InkWell(
                    onTap: () => context.go('/profile'),
                    customBorder: const CircleBorder(),
                    child: InitialsAvatar(initials: auth.initials, size: 40),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.lg),
              if (isDonor) const _DonorHome() else const _StaffHome(),
            ],
          ),
        ),
      ),
    );
  }
}

class _DonorHome extends ConsumerWidget {
  const _DonorHome();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final appointments = ref.watch(myAppointmentsProvider);
    final eligibility = ref.watch(eligibilityProvider).valueOrNull;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _StatsRow(appointments: appointments),
        const SizedBox(height: AppSpacing.xl),
        const SectionLabel('Appointments'),
        _ActionGroup(
          rows: [
            _ActionRow(
              icon: Icons.event_available_outlined,
              title: 'My donations',
              subtitle: 'View, book and manage your appointments',
              onTap: () => context.go('/appointments'),
            ),
            _ActionRow(
              icon: Icons.add_circle_outline,
              title: 'Book a donation',
              subtitle: 'Pick a blood bank and a time',
              onTap: () => context.push('/appointments/book'),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.xl),
        const SectionLabel('For you'),
        _ActionGroup(
          rows: [
            _ActionRow(
              icon: Icons.bloodtype_outlined,
              title: 'Requests near you',
              subtitle: 'Hospitals within 50 km that need your blood type',
              onTap: () => context.go('/blood-requests'),
            ),
            _ActionRow(
              icon: Icons.verified_outlined,
              title: 'My eligibility',
              subtitle: eligibilitySummary(eligibility),
              onTap: () => context.push('/eligibility'),
            ),
            _ActionRow(
              icon: Icons.map_outlined,
              title: 'Nearby blood banks',
              subtitle: 'Find a place to donate',
              onTap: () => context.push('/nearby-banks'),
            ),
          ],
        ),
      ],
    );
  }
}

class _StaffHome extends ConsumerWidget {
  const _StaffHome();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final requests = ref.watch(bloodRequestsProvider);
    int count(String status) =>
        requests.valueOrNull
            ?.where((r) => normalizeBloodLabel(r.status) == status)
            .length ??
        0;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: _StatCard(
                icon: Icons.bloodtype_outlined,
                iconColor: AppColors.primary,
                label: 'Open',
                value: count('open'),
                loading: requests.isLoading,
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: _StatCard(
                icon: Icons.how_to_reg_outlined,
                iconColor: AppColors.scheduled,
                label: 'To approve',
                value: count('awaiting_approval'),
                loading: requests.isLoading,
              ),
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.xl),
        const SectionLabel('Blood requests'),
        _ActionGroup(
          rows: [
            _ActionRow(
              icon: Icons.list_alt_outlined,
              title: 'All requests',
              subtitle: 'Track requests and their coordinator workflows',
              onTap: () => context.go('/blood-requests'),
            ),
            _ActionRow(
              icon: Icons.add_circle_outline,
              title: 'New blood request',
              subtitle: 'Start the agents on a new request',
              onTap: () async {
                final created = await context.push<bool>('/blood-requests/new');
                if (created == true) ref.invalidate(bloodRequestsProvider);
              },
            ),
          ],
        ),
        const SizedBox(height: AppSpacing.xl),
        const SectionLabel('Network'),
        _ActionGroup(
          rows: [
            _ActionRow(
              icon: Icons.map_outlined,
              title: 'Nearby blood banks',
              subtitle: 'Blood banks and hospitals on the map',
              onTap: () => context.push('/nearby-banks'),
            ),
          ],
        ),
      ],
    );
  }
}

class _ActionGroup extends StatelessWidget {
  const _ActionGroup({required this.rows});

  final List<_ActionRow> rows;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          for (var i = 0; i < rows.length; i++) ...[
            if (i > 0)
              const Divider(
                height: 1,
                indent: AppSpacing.md + 32 + AppSpacing.sm,
              ),
            FadeSlideIn(index: i, child: rows[i]),
          ],
        ],
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
    final upcoming = appointments.valueOrNull
        ?.where((a) => a.isUpcoming)
        .length;

    final completed = appointments.valueOrNull
        ?.where((a) => a.status == 'completed')
        .length;

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
  const _StatCard({
    required this.icon,
    required this.iconColor,
    required this.label,
    required this.value,
    required this.loading,
  });

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
          Expanded(
            child: Text(
              label,
              style: AppText.bodySmall,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }
}

// ── grouped-list action row ─────────────────────────────────────────────

class _ActionRow extends StatelessWidget {
  const _ActionRow({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.md,
            vertical: AppSpacing.sm,
          ),
          child: Row(
            children: [
              Container(
                width: 32,
                height: 32,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: AppColors.primarySubtle,
                  borderRadius: BorderRadius.circular(AppRadii.sm - 2),
                ),
                child: Icon(icon, size: 17, color: AppColors.primary),
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
              const Icon(
                Icons.chevron_right,
                size: 20,
                color: AppColors.inkFaint,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Bell icon with an unread-count badge, opening the notification inbox.
class _NotificationBell extends ConsumerWidget {
  const _NotificationBell();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unread = ref.watch(unreadNotificationCountProvider).valueOrNull ?? 0;

    return InkWell(
      onTap: () => context.push('/notifications'),
      customBorder: const CircleBorder(),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xxs),
        child: Stack(
          clipBehavior: Clip.none,
          children: [
            const Icon(
              Icons.notifications_none,
              size: 24,
              color: AppColors.ink,
            ),
            if (unread > 0)
              Positioned(
                right: -2,
                top: -2,
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 4,
                    vertical: 1,
                  ),
                  constraints: const BoxConstraints(minWidth: 16),
                  decoration: const BoxDecoration(
                    color: AppColors.primary,
                    shape: BoxShape.circle,
                  ),
                  child: Text(
                    unread > 9 ? '9+' : '$unread',
                    style: AppText.caption.copyWith(
                      color: Colors.white,
                      fontSize: 9,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
