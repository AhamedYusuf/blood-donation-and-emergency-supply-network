import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/date_format.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/agent_tag.dart';
import '../../widgets/app_card.dart';
import '../../widgets/brand_mark.dart';
import '../../widgets/states.dart';
import '../../widgets/status_pill.dart';
import 'appointment.dart';
import 'appointments_repository.dart';

class MyAppointmentsScreen extends ConsumerWidget {
  const MyAppointmentsScreen({super.key});

  Future<void> _refresh(WidgetRef ref) async {
    ref.invalidate(myAppointmentsProvider);
    await ref.read(myAppointmentsProvider.future);
  }

  Future<void> _book(BuildContext context, WidgetRef ref) async {
    await context.push('/appointments/book');
    ref.invalidate(myAppointmentsProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(myAppointmentsProvider);

    return Scaffold(
      appBar: AppBar(
        titleSpacing: AppSpacing.gutter,
        title: Row(
          children: [
            const BrandMark(size: 22, color: AppColors.ink),
            const SizedBox(width: AppSpacing.xs),
            Text('My donations', style: AppText.title),
          ],
        ),
      ),
      floatingActionButton: async.maybeWhen(
        data: (list) => list.isEmpty
            ? null
            : FloatingActionButton.extended(
                onPressed: () => _book(context, ref),
                backgroundColor: AppColors.primary,
                foregroundColor: AppColors.onPrimary,
                elevation: 2,
                icon: const Icon(Icons.add, size: 20),
                label: Text('Book', style: AppText.button),
              ),
        orElse: () => null,
      ),
      body: async.when(
        loading: () => const AppointmentsSkeleton(),
        error: (err, _) => ErrorState(
          message: err is ApiException ? err.message : 'We couldn’t load your donations.',
          onRetry: () => _refresh(ref),
        ),
        data: (appointments) {
          if (appointments.isEmpty) {
            return RefreshIndicator(
              color: AppColors.primary,
              onRefresh: () => _refresh(ref),
              child: LayoutBuilder(
                builder: (context, c) => SingleChildScrollView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  child: ConstrainedBox(
                    constraints: BoxConstraints(minHeight: c.maxHeight),
                    child: EmptyState(
                      icon: Icons.water_drop_outlined,
                      title: 'No donations booked',
                      message: 'Schedule your first donation — it only takes a minute.',
                      action: FilledButton(
                        onPressed: () => _book(context, ref),
                        style: FilledButton.styleFrom(minimumSize: const Size(200, 48)),
                        child: const Text('Book a donation'),
                      ),
                    ),
                  ),
                ),
              ),
            );
          }

          final upcoming = appointments.where((a) => a.isUpcoming).toList()
            ..sort((a, b) => a.scheduledTime.compareTo(b.scheduledTime));
          final history = appointments.where((a) => !a.isUpcoming).toList();
          final next = upcoming.isNotEmpty ? upcoming.first : null;
          final laterUpcoming = upcoming.skip(1).toList();

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () => _refresh(ref),
            child: ListView(
              padding: const EdgeInsets.fromLTRB(
                  AppSpacing.gutter, AppSpacing.xs, AppSpacing.gutter, 96),
              children: [
                const SectionLabel('Next donation'),
                if (next != null)
                  _HeroCard(appointment: next, onCancel: () => _confirmCancel(context, ref, next))
                else
                  _BookPrompt(onBook: () => _book(context, ref)),

                if (laterUpcoming.isNotEmpty) ...[
                  const SizedBox(height: AppSpacing.lg),
                  const SectionLabel('Also upcoming'),
                  for (final a in laterUpcoming)
                    Padding(
                      padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                      child: _Row(appointment: a, onCancel: () => _confirmCancel(context, ref, a)),
                    ),
                ],

                if (history.isNotEmpty) ...[
                  const SizedBox(height: AppSpacing.lg),
                  SectionLabel('History',
                      trailing: Text('${history.length}', style: AppText.caption)),
                  for (final a in history)
                    Padding(
                      padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                      child: _Row(appointment: a, onCancel: null),
                    ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }

  Future<void> _confirmCancel(BuildContext context, WidgetRef ref, Appointment a) async {
    final ok = await showModalBottomSheet<bool>(
      context: context,
      builder: (ctx) => _CancelSheet(appointment: a),
    );
    if (ok != true) return;

    try {
      await ref.read(appointmentsRepositoryProvider).cancel(a.id);
      ref.invalidate(myAppointmentsProvider);
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Appointment cancelled')));
      }
    } on ApiException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }
}

// ── hero ─────────────────────────────────────────────────────────────────

class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.appointment, required this.onCancel});
  final Appointment appointment;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    final a = appointment;
    return AppCard(
      elevated: true,
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _DateBlock(a.scheduledTime),
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(clockTime(a.scheduledTime),
                        style: AppText.numeric.copyWith(fontSize: 17, fontWeight: FontWeight.w600)),
                    const SizedBox(height: 2),
                    Text(relativeTime(a.scheduledTime),
                        style: AppText.bodySmall.copyWith(color: AppColors.inkMuted)),
                    const SizedBox(height: AppSpacing.sm),
                    Wrap(
                      spacing: AppSpacing.xs,
                      runSpacing: AppSpacing.xs,
                      children: [
                        StatusPill(a.status),
                        if (a.donorBloodType != null) _BloodTypeChip(a.donorBloodType!),
                        if (a.isAgentMatched) const AgentTag(),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (a.canCancel) ...[
            const SizedBox(height: AppSpacing.md),
            const Divider(height: 1),
            const SizedBox(height: AppSpacing.xs),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                onPressed: onCancel,
                style: TextButton.styleFrom(
                  foregroundColor: AppColors.critical,
                  padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs, vertical: AppSpacing.xs),
                ),
                icon: const Icon(Icons.close, size: 16),
                label: const Text('Cancel appointment'),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _DateBlock extends StatelessWidget {
  const _DateBlock(this.dt);
  final DateTime dt;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 58,
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
      decoration: BoxDecoration(
        color: AppColors.primarySubtle,
        borderRadius: BorderRadius.circular(AppRadii.sm),
      ),
      child: Column(
        children: [
          Text(weekdayAbbr(dt).toUpperCase(),
              style: AppText.caption.copyWith(color: AppColors.primary, letterSpacing: 0.6)),
          Text(dayNum(dt), style: AppText.heroFigure.copyWith(color: AppColors.primary)),
          Text(monthAbbr(dt).toUpperCase(),
              style: AppText.caption.copyWith(color: AppColors.primary, letterSpacing: 0.6)),
        ],
      ),
    );
  }
}

class _BookPrompt extends StatelessWidget {
  const _BookPrompt({required this.onBook});
  final VoidCallback onBook;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      elevated: true,
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Nothing booked', style: AppText.headline),
          const SizedBox(height: AppSpacing.xxs),
          Text('You have no upcoming donation. Book a slot at a nearby blood bank.',
              style: AppText.bodySmall),
          const SizedBox(height: AppSpacing.md),
          FilledButton(onPressed: onBook, child: const Text('Book a donation')),
        ],
      ),
    );
  }
}

// ── list row ─────────────────────────────────────────────────────────────

class _Row extends StatelessWidget {
  const _Row({required this.appointment, required this.onCancel});
  final Appointment appointment;
  final VoidCallback? onCancel;

  @override
  Widget build(BuildContext context) {
    final a = appointment;
    final s = statusStyle(a.status);
    return AppCard(
      accent: s.color,
      padding: const EdgeInsets.fromLTRB(AppSpacing.sm, AppSpacing.sm, AppSpacing.xs, AppSpacing.sm),
      child: Row(
        children: [
          SizedBox(
            width: 44,
            child: Column(
              children: [
                Text(dayNum(a.scheduledTime), style: AppText.numeric.copyWith(fontSize: 17)),
                Text(monthAbbr(a.scheduledTime).toUpperCase(), style: AppText.caption),
              ],
            ),
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(clockTime(a.scheduledTime), style: AppText.numeric),
                const SizedBox(height: 4),
                Wrap(
                  spacing: AppSpacing.xs,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    StatusPill(a.status),
                    if (a.status == 'completed' && a.unitsDonated != null)
                      Text('${a.unitsDonated} unit${a.unitsDonated == 1 ? '' : 's'}',
                          style: AppText.caption.copyWith(color: AppColors.inkMuted)),
                    if (a.isAgentMatched) const AgentTag(),
                  ],
                ),
              ],
            ),
          ),
          if (onCancel != null && a.canCancel)
            IconButton(
              tooltip: 'Cancel',
              visualDensity: VisualDensity.compact,
              icon: const Icon(Icons.close, size: 18, color: AppColors.inkMuted),
              onPressed: onCancel,
            ),
        ],
      ),
    );
  }
}

class _BloodTypeChip extends StatelessWidget {
  const _BloodTypeChip(this.type);
  final String type;
  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: AppColors.surfaceSunken,
        borderRadius: BorderRadius.circular(AppRadii.pill),
      ),
      child: Text(type, style: AppText.caption.copyWith(color: AppColors.ink, letterSpacing: 0.2)),
    );
  }
}

// ── cancel sheet ─────────────────────────────────────────────────────────

class _CancelSheet extends StatelessWidget {
  const _CancelSheet({required this.appointment});
  final Appointment appointment;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.xs, AppSpacing.lg, AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Cancel this appointment?', style: AppText.headline),
            const SizedBox(height: AppSpacing.xs),
            Text(
              '${longStamp(appointment.scheduledTime)}. The slot is released and '
              'you can book another any time.',
              style: AppText.bodySmall,
            ),
            const SizedBox(height: AppSpacing.lg),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              style: FilledButton.styleFrom(backgroundColor: AppColors.critical),
              child: const Text('Cancel appointment'),
            ),
            const SizedBox(height: AppSpacing.xs),
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Keep it'),
            ),
          ],
        ),
      ),
    );
  }
}
