import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import 'appointment.dart';
import 'appointments_repository.dart';

class MyAppointmentsScreen extends ConsumerWidget {
  const MyAppointmentsScreen({super.key});

  Future<void> _refresh(WidgetRef ref) async {
    ref.invalidate(myAppointmentsProvider);
    await ref.read(myAppointmentsProvider.future);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(myAppointmentsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('My Appointments')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          await context.push('/appointments/book');
          ref.invalidate(myAppointmentsProvider);
        },
        icon: const Icon(Icons.add),
        label: const Text('Book'),
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (err, _) => _ErrorView(
          message: err is ApiException ? err.message : 'Could not load appointments.',
          onRetry: () => _refresh(ref),
        ),
        data: (appointments) {
          final upcoming = appointments.where((a) => a.isUpcoming).toList()
            ..sort((a, b) => a.scheduledTime.compareTo(b.scheduledTime));
          final past = appointments.where((a) => !a.isUpcoming).toList();

          return RefreshIndicator(
            onRefresh: () => _refresh(ref),
            child: appointments.isEmpty
                ? const _EmptyView()
                : ListView(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    children: [
                      if (upcoming.isNotEmpty) ...[
                        const _SectionHeader('Upcoming'),
                        for (final a in upcoming)
                          _AppointmentTile(appointment: a, cancellable: true),
                      ],
                      if (past.isNotEmpty) ...[
                        const _SectionHeader('History'),
                        for (final a in past)
                          _AppointmentTile(appointment: a, cancellable: false),
                      ],
                    ],
                  ),
          );
        },
      ),
    );
  }
}

class _AppointmentTile extends ConsumerWidget {
  const _AppointmentTile({required this.appointment, required this.cancellable});

  final Appointment appointment;
  final bool cancellable;

  Future<void> _cancel(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Cancel appointment?'),
        content: const Text('This frees the slot. You can book another later.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Keep'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Cancel it'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    try {
      await ref.read(appointmentsRepositoryProvider).cancel(appointment.id);
      ref.invalidate(myAppointmentsProvider);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Appointment cancelled')),
        );
      }
    } on ApiException catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(e.message)));
      }
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = appointment.scheduledTime;
    final dateLabel =
        '${_weekday(t.weekday)} ${t.day} ${_month(t.month)}, '
        '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

    return ListTile(
      leading: _StatusDot(status: appointment.status),
      title: Text(dateLabel),
      subtitle: Row(
        children: [
          Text(_statusLabel(appointment.status)),
          if (appointment.isAgentMatched) ...[
            const SizedBox(width: 8),
            const _AgentChip(),
          ],
          if (appointment.unitsDonated != null) ...[
            const SizedBox(width: 8),
            Text('${appointment.unitsDonated} unit(s)'),
          ],
        ],
      ),
      trailing: cancellable && appointment.canCancel
          ? IconButton(
              tooltip: 'Cancel',
              icon: const Icon(Icons.close),
              onPressed: () => _cancel(context, ref),
            )
          : null,
    );
  }
}

class _StatusDot extends StatelessWidget {
  const _StatusDot({required this.status});
  final String status;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 10,
      height: 10,
      margin: const EdgeInsets.only(top: 6),
      decoration: BoxDecoration(
        color: _statusColor(status, context),
        shape: BoxShape.circle,
      ),
    );
  }
}

class _AgentChip extends StatelessWidget {
  const _AgentChip();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
      decoration: BoxDecoration(
        color: const Color(0xFF7C5CFC).withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(4),
      ),
      child: const Text(
        'Agent',
        style: TextStyle(fontSize: 11, color: Color(0xFF7C5CFC)),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Text(
        text.toUpperCase(),
        style: Theme.of(context)
            .textTheme
            .labelSmall
            ?.copyWith(letterSpacing: 0.5, color: Theme.of(context).hintColor),
      ),
    );
  }
}

class _EmptyView extends StatelessWidget {
  const _EmptyView();

  @override
  Widget build(BuildContext context) {
    return ListView(
      children: [
        const SizedBox(height: 120),
        Icon(Icons.event_available, size: 48, color: Theme.of(context).hintColor),
        const SizedBox(height: 12),
        const Center(child: Text('No appointments yet')),
        const SizedBox(height: 4),
        Center(
          child: Text(
            'Tap Book to schedule a donation.',
            style: TextStyle(color: Theme.of(context).hintColor),
          ),
        ),
      ],
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: onRetry, child: const Text('Retry')),
          ],
        ),
      ),
    );
  }
}

Color _statusColor(String status, BuildContext context) {
  switch (status) {
    case 'scheduled':
      return const Color(0xFFD97F1D);
    case 'completed':
      return const Color(0xFF1F8A54);
    case 'no_show':
      return const Color(0xFFD64545);
    case 'cancelled':
    default:
      return Theme.of(context).hintColor;
  }
}

String _statusLabel(String status) => switch (status) {
      'scheduled' => 'Scheduled',
      'completed' => 'Completed',
      'no_show' => 'No-show',
      'cancelled' => 'Cancelled',
      _ => status,
    };

String _weekday(int w) =>
    const ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'][w - 1];
String _month(int m) => const [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ][m - 1];
