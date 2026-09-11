import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../theme/tokens.dart';

/// Single source of truth for appointment-status colour + label. The pill
/// and the card's left accent bar both read from here, so they can never
/// disagree.
class StatusStyle {
  const StatusStyle(this.label, this.color, this.subtle);
  final String label;
  final Color color;
  final Color subtle;
}

StatusStyle statusStyle(String status) => switch (status) {
      'scheduled' => const StatusStyle('Scheduled', AppColors.scheduled, AppColors.scheduledSubtle),
      'completed' => const StatusStyle('Completed', AppColors.success, AppColors.successSubtle),
      'no_show' => const StatusStyle('No-show', AppColors.critical, AppColors.criticalSubtle),
      'cancelled' => const StatusStyle('Cancelled', AppColors.neutral, AppColors.neutralSubtle),
      _ => StatusStyle(status, AppColors.neutral, AppColors.neutralSubtle),
    };

/// A status as colour + word — never colour alone.
class StatusPill extends StatelessWidget {
  const StatusPill(this.status, {super.key});
  final String status;

  @override
  Widget build(BuildContext context) {
    final s = statusStyle(status);
    return Container(
      padding: const EdgeInsets.fromLTRB(8, 3, 10, 3),
      decoration: BoxDecoration(
        color: s.subtle,
        borderRadius: BorderRadius.circular(AppRadii.pill),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(width: 6, height: 6, decoration: BoxDecoration(color: s.color, shape: BoxShape.circle)),
          const SizedBox(width: 6),
          Text(s.label, style: AppText.caption.copyWith(color: s.color, letterSpacing: 0.2)),
        ],
      ),
    );
  }
}
