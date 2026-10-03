import 'package:flutter/material.dart';

import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';

class BloodRequestStatusPill extends StatelessWidget {
  const BloodRequestStatusPill(this.status, {super.key});

  final String status;

  @override
  Widget build(BuildContext context) {
    final normalized = normalizeBloodLabel(status);
    final isSuccess = const {
      'fulfilled',
      'completed',
      'complete',
      'approved',
    }.contains(normalized);
    final isFailure = const {
      'rejected',
      'failed',
      'error',
      'cancelled',
      'canceled',
      'expired',
    }.contains(normalized);
    final isAgent = const {
      'matching',
      'donors_notified',
      'dispatch',
      'in_progress',
      'running',
    }.contains(normalized);
    final color = isSuccess
        ? AppColors.success
        : isFailure
        ? AppColors.critical
        : isAgent
        ? AppColors.agent
        : AppColors.scheduled;
    final background = isSuccess
        ? AppColors.successSubtle
        : isFailure
        ? AppColors.criticalSubtle
        : isAgent
        ? AppColors.agentSubtle
        : AppColors.scheduledSubtle;

    return _LabelPill(
      label: bloodLabel(status),
      color: color,
      background: background,
    );
  }
}

class BloodRequestUrgencyPill extends StatelessWidget {
  const BloodRequestUrgencyPill(this.urgency, {super.key});

  final String urgency;

  @override
  Widget build(BuildContext context) {
    final normalized = normalizeBloodLabel(urgency);
    final critical = const {'critical', 'emergency'}.contains(normalized);
    final urgent = const {'urgent', 'high'}.contains(normalized);

    return _LabelPill(
      label: bloodLabel(urgency),
      color: critical
          ? AppColors.critical
          : urgent
          ? AppColors.scheduled
          : AppColors.inkMuted,
      background: critical
          ? AppColors.criticalSubtle
          : urgent
          ? AppColors.scheduledSubtle
          : AppColors.neutralSubtle,
    );
  }
}

class _LabelPill extends StatelessWidget {
  const _LabelPill({
    required this.label,
    required this.color,
    required this.background,
  });

  final String label;
  final Color color;
  final Color background;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(AppRadii.pill),
      ),
      child: Text(
        label,
        style: AppText.caption.copyWith(color: color, letterSpacing: 0),
      ),
    );
  }
}

String normalizeBloodLabel(String value) =>
    value.trim().toLowerCase().replaceAll('-', '_').replaceAll(' ', '_');

String bloodLabel(String value) => value
    .split(RegExp(r'[_\s]+'))
    .where((part) => part.isNotEmpty)
    .map((part) => '${part[0].toUpperCase()}${part.substring(1)}')
    .join(' ');
