import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import '../theme/tokens.dart';

/// Marks data that came from the Matching & Dispatch agent. The violet
/// accent means exactly this and nothing else.
class AgentTag extends StatelessWidget {
  const AgentTag({super.key});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: AppColors.agentSubtle,
        borderRadius: BorderRadius.circular(AppRadii.sm - 4),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.auto_awesome, size: 11, color: AppColors.agent),
          const SizedBox(width: 4),
          Text('Agent match', style: AppText.caption.copyWith(color: AppColors.agent, letterSpacing: 0.2)),
        ],
      ),
    );
  }
}
