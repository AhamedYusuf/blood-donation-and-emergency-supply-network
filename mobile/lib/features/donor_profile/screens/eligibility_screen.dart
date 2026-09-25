import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../theme/app_theme.dart';
import '../../../theme/tokens.dart';
import '../providers/donor_profile_provider.dart';

class EligibilityScreen extends ConsumerWidget {
  const EligibilityScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final result = ref.watch(eligibilityProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Eligibility')),
      body: result.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, _) => Center(child: TextButton(onPressed: () => ref.invalidate(eligibilityProvider), child: const Text('Retry'))),
        data: (eligibility) => RefreshIndicator(
          onRefresh: () async => ref.invalidate(eligibilityProvider),
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(AppSpacing.gutter),
            children: [
              const SizedBox(height: AppSpacing.xxl),
              Icon(eligibility.isEligible ? Icons.check_circle : Icons.info, size: 84, color: eligibility.isEligible ? AppColors.success : AppColors.scheduled),
              const SizedBox(height: AppSpacing.lg),
              Text(eligibility.isEligible ? 'You are eligible to donate' : 'You are not eligible yet', style: AppText.title, textAlign: TextAlign.center),
              if (eligibility.reason != null) ...[
                const SizedBox(height: AppSpacing.md),
                Text(eligibility.reason!, style: AppText.body, textAlign: TextAlign.center),
              ],
              if (eligibility.daysUntilEligible != null) ...[
                const SizedBox(height: AppSpacing.sm),
                Text('${eligibility.daysUntilEligible} days until you are eligible again', style: AppText.bodyStrong, textAlign: TextAlign.center),
              ],
            ],
          ),
        ),
      ),
    );
  }
}