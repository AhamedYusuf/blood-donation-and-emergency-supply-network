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
import '../../widgets/fade_slide_in.dart';
import '../../widgets/states.dart';
import '../../widgets/status_pill.dart';
import '../../widgets/ticket_card.dart';
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
                  _HeroCard(
                    appointment: next,
                    onCancel: () => _confirmCancel(context, ref, next),
                    onConfirm: () => _respondToPending(context, ref, next, accept: true),
                    onDecline: () => _respondToPending(context, ref, next, accept: false),
                    onReschedule: () => _reschedule(context, ref, next),
                  )
                else
                  _BookPrompt(onBook: () => _book(context, ref)),

                if (laterUpcoming.isNotEmpty) ...[
                  const SizedBox(height: AppSpacing.lg),
                  const SectionLabel('Also upcoming'),
                  for (final (i, a) in laterUpcoming.indexed)
                    Padding(
                      padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                      child: FadeSlideIn(
                        index: i,
                        child: _Row(
                          appointment: a,
                          onCancel: () => _confirmCancel(context, ref, a),
                          onConfirm: () => _respondToPending(context, ref, a, accept: true),
                          onDecline: () => _respondToPending(context, ref, a, accept: false),
                          onReschedule: () => _reschedule(context, ref, a),
                        ),
                      ),
                    ),
                ],

                if (history.isNotEmpty) ...[
                  const SizedBox(height: AppSpacing.lg),
                  SectionLabel('History',
                      trailing: Text('${history.length}', style: AppText.caption)),
                  for (final (i, a) in history.indexed)
                    Padding(
                      padding: const EdgeInsets.only(bottom: AppSpacing.xs),
                      child: FadeSlideIn(index: i, child: _Row(appointment: a, onCancel: null)),
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

  Future<void> _respondToPending(
    BuildContext context,
    WidgetRef ref,
    Appointment a, {
    required bool accept,
  }) async {
    if (!accept) {
      final ok = await showModalBottomSheet<bool>(
        context: context,
        builder: (ctx) => _DeclineSheet(appointment: a),
      );
      if (ok != true) return;
    }

    try {
      final repo = ref.read(appointmentsRepositoryProvider);
      if (accept) {
        await repo.confirm(a.id);
      } else {
        await repo.decline(a.id);
      }
      ref.invalidate(myAppointmentsProvider);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
          content: Text(accept ? 'Appointment confirmed' : 'Appointment declined'),
        ));
      }
    } on ApiException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }

  Future<void> _reschedule(BuildContext context, WidgetRef ref, Appointment a) async {
    final newTime = await showModalBottomSheet<DateTime>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => _RescheduleSheet(appointment: a),
    );
    if (newTime == null) return;

    try {
      await ref.read(appointmentsRepositoryProvider).reschedule(a.id, newTime);
      ref.invalidate(myAppointmentsProvider);
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Appointment rescheduled')));
      }
    } on ApiException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }
}

// ── hero ─────────────────────────────────────────────────────────────────

/// The one boarding-pass surface in the product (see [TicketCard]) — the
/// donor's single most important fact, front and centre in the brand
/// gradient rather than another flat white card.
class _HeroCard extends StatelessWidget {
  const _HeroCard({
    required this.appointment,
    required this.onCancel,
    required this.onConfirm,
    required this.onDecline,
    required this.onReschedule,
  });
  final Appointment appointment;
  final VoidCallback onCancel;
  final VoidCallback onConfirm;
  final VoidCallback onDecline;
  final VoidCallback onReschedule;

  @override
  Widget build(BuildContext context) {
    final a = appointment;
    return TicketCard(
      gradient: AppGradients.ticket,
      boxShadow: AppElevation.lifted,
      bottom: a.canConfirmOrDecline
          ? Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.xxs, AppSpacing.lg, AppSpacing.sm),
              child: Row(
                children: [
                  Expanded(
                    child: FilledButton(
                      onPressed: onConfirm,
                      style: FilledButton.styleFrom(
                        backgroundColor: Colors.white,
                        foregroundColor: AppColors.primary,
                        minimumSize: const Size(0, 40),
                      ),
                      child: const Text('Accept'),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Expanded(
                    child: OutlinedButton(
                      onPressed: onReschedule,
                      style: OutlinedButton.styleFrom(
                        foregroundColor: Colors.white,
                        side: const BorderSide(color: Colors.white),
                        minimumSize: const Size(0, 40),
                      ),
                      child: const Text('Reschedule'),
                    ),
                  ),
                  IconButton(
                    onPressed: onDecline,
                    tooltip: 'Decline',
                    icon: const Icon(Icons.close, color: Colors.white),
                  ),
                ],
              ),
            )
          : a.canCancel
              ? Padding(
                  padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.xxs, AppSpacing.lg, AppSpacing.sm),
                  child: Row(
                    children: [
                      TextButton.icon(
                        onPressed: onCancel,
                        style: TextButton.styleFrom(
                          foregroundColor: AppColors.critical,
                          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs, vertical: AppSpacing.xs),
                        ),
                        icon: const Icon(Icons.close, size: 16),
                        label: const Text('Cancel'),
                      ),
                      const Spacer(),
                      TextButton.icon(
                        onPressed: onReschedule,
                        style: TextButton.styleFrom(
                          foregroundColor: Colors.white,
                          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs, vertical: AppSpacing.xs),
                        ),
                        icon: const Icon(Icons.schedule, size: 16),
                        label: const Text('Reschedule'),
                      ),
                    ],
                  ),
                )
              : null,
      top: Padding(
        padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.lg, AppSpacing.lg, AppSpacing.md),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _DateBlock(a.scheduledTime),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(clockTime(a.scheduledTime),
                      style: AppText.numeric.copyWith(fontSize: 20, fontWeight: FontWeight.w600, color: Colors.white)),
                  const SizedBox(height: 2),
                  Text(relativeTime(a.scheduledTime),
                      style: AppText.bodySmall.copyWith(color: Colors.white.withValues(alpha: 0.68))),
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
        color: Colors.white.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(AppRadii.sm),
        border: Border.all(color: Colors.white.withValues(alpha: 0.22)),
      ),
      child: Column(
        children: [
          Text(weekdayAbbr(dt).toUpperCase(),
              style: AppText.caption.copyWith(color: Colors.white.withValues(alpha: 0.8), letterSpacing: 0.6)),
          Text(dayNum(dt), style: AppText.heroFigure.copyWith(color: Colors.white)),
          Text(monthAbbr(dt).toUpperCase(),
              style: AppText.caption.copyWith(color: Colors.white.withValues(alpha: 0.8), letterSpacing: 0.6)),
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
  const _Row({
    required this.appointment,
    required this.onCancel,
    this.onConfirm,
    this.onDecline,
    this.onReschedule,
  });
  final Appointment appointment;
  final VoidCallback? onCancel;
  final VoidCallback? onConfirm;
  final VoidCallback? onDecline;
  final VoidCallback? onReschedule;

  @override
  Widget build(BuildContext context) {
    final a = appointment;
    final s = statusStyle(a.status);

    final menuEntries = <PopupMenuEntry<VoidCallback>>[
      if (onConfirm != null && a.canConfirmOrDecline)
        PopupMenuItem(value: onConfirm, child: const Text('Accept')),
      if (onDecline != null && a.canConfirmOrDecline)
        PopupMenuItem(value: onDecline, child: const Text('Decline')),
      if (onReschedule != null && a.canReschedule)
        PopupMenuItem(value: onReschedule, child: const Text('Reschedule')),
      if (onCancel != null && a.canCancel)
        PopupMenuItem(value: onCancel, child: const Text('Cancel')),
    ];

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
          if (menuEntries.isNotEmpty)
            PopupMenuButton<VoidCallback>(
              tooltip: 'Actions',
              icon: const Icon(Icons.more_vert, size: 18, color: AppColors.inkMuted),
              itemBuilder: (_) => menuEntries,
              onSelected: (action) => action(),
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

// ── decline sheet ────────────────────────────────────────────────────────

class _DeclineSheet extends StatelessWidget {
  const _DeclineSheet({required this.appointment});
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
            Text('Decline this donation?', style: AppText.headline),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'You were matched for ${longStamp(appointment.scheduledTime)}. Declining '
              "releases the slot so it can be offered to someone else — you can't undo this, "
              'but you can always book or get matched again later.',
              style: AppText.bodySmall,
            ),
            const SizedBox(height: AppSpacing.lg),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              style: FilledButton.styleFrom(backgroundColor: AppColors.critical),
              child: const Text('Decline'),
            ),
            const SizedBox(height: AppSpacing.xs),
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Keep it pending'),
            ),
          ],
        ),
      ),
    );
  }
}

// ── reschedule sheet ─────────────────────────────────────────────────────

class _RescheduleSheet extends StatefulWidget {
  const _RescheduleSheet({required this.appointment});
  final Appointment appointment;

  @override
  State<_RescheduleSheet> createState() => _RescheduleSheetState();
}

class _RescheduleSheetState extends State<_RescheduleSheet> {
  DateTime? _date;
  TimeOfDay? _time;

  DateTime? get _slot => (_date != null && _time != null)
      ? DateTime(_date!.year, _date!.month, _date!.day, _time!.hour, _time!.minute)
      : null;

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final d = await showDatePicker(
      context: context,
      initialDate: _date ?? widget.appointment.scheduledTime,
      firstDate: now,
      lastDate: now.add(const Duration(days: 90)),
    );
    if (d != null) setState(() => _date = d);
  }

  Future<void> _pickTime() async {
    final t = await showTimePicker(
      context: context,
      initialTime: _time ?? TimeOfDay.fromDateTime(widget.appointment.scheduledTime),
    );
    if (t != null) setState(() => _time = t);
  }

  @override
  Widget build(BuildContext context) {
    final slot = _slot;
    final valid = slot != null && slot.isAfter(DateTime.now());

    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppSpacing.lg, AppSpacing.xs, AppSpacing.lg, AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Pick a new time', style: AppText.headline),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Currently ${longStamp(widget.appointment.scheduledTime)}.',
              style: AppText.bodySmall,
            ),
            const SizedBox(height: AppSpacing.md),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: _pickDate,
                    icon: const Icon(Icons.calendar_today_outlined, size: 16),
                    label: Text(_date == null
                        ? 'Date'
                        : '${weekdayAbbr(_date!)} ${_date!.day} ${monthAbbr(_date!)}'),
                  ),
                ),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: _pickTime,
                    icon: const Icon(Icons.schedule_outlined, size: 16),
                    label: Text(_time?.format(context) ?? 'Time'),
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.lg),
            FilledButton(
              onPressed: valid ? () => Navigator.pop(context, slot) : null,
              child: const Text('Confirm new time'),
            ),
            const SizedBox(height: AppSpacing.xs),
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Cancel'),
            ),
          ],
        ),
      ),
    );
  }
}
