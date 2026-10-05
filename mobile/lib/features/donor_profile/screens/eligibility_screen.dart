import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../theme/app_theme.dart';
import '../../../theme/tokens.dart';
import '../../../widgets/app_card.dart';
import '../../../widgets/states.dart';
import '../eligibility_text.dart';
import '../providers/donor_profile_provider.dart';

class EligibilityScreen extends ConsumerStatefulWidget {
  const EligibilityScreen({super.key});

  @override
  ConsumerState<EligibilityScreen> createState() => _EligibilityScreenState();
}

class _EligibilityScreenState extends ConsumerState<EligibilityScreen> {
  @override
  void initState() {
    super.initState();
    // Home and Profile keep the result alive in the background, so fetch a
    // fresh one: an admin may have verified the donor since it was loaded.
    Future.microtask(() => ref.invalidate(donorProfileProvider));
  }

  @override
  Widget build(BuildContext context) {
    final result = ref.watch(eligibilityProvider);
    return Scaffold(
      backgroundColor: AppColors.canvas,
      appBar: AppBar(title: const Text('My eligibility')),
      body: result.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, _) => ErrorState(
          message: 'We couldn’t check your eligibility.',
          onRetry: () => ref.invalidate(eligibilityProvider),
        ),
        data: (eligibility) {
          final eligible = eligibility.isEligible;
          final color = eligible ? AppColors.success : AppColors.scheduled;
          final days = eligibility.daysUntilEligible;
          final illness = (eligibility.reason ?? '').startsWith('medical_flags');
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () async => ref.invalidate(donorProfileProvider),
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(AppSpacing.gutter),
              children: [
                AppCard(
                  padding: const EdgeInsets.all(AppSpacing.lg),
                  child: Column(
                    children: [
                      Container(
                        width: 64,
                        height: 64,
                        decoration: BoxDecoration(
                          color: color.withValues(alpha: 0.12),
                          shape: BoxShape.circle,
                        ),
                        child: Icon(
                          eligible ? Icons.check_rounded : Icons.schedule_rounded,
                          size: 34,
                          color: color,
                        ),
                      ),
                      const SizedBox(height: AppSpacing.md),
                      Text(
                        eligible ? 'You can donate now' : 'You can’t donate yet',
                        style: AppText.title,
                        textAlign: TextAlign.center,
                      ),
                      const SizedBox(height: AppSpacing.xs),
                      Text(
                        eligibilityExplanation(eligibility.reason),
                        style: AppText.bodySmall,
                        textAlign: TextAlign.center,
                      ),
                      if (!eligible && days != null && days > 0) ...[
                        const SizedBox(height: AppSpacing.md),
                        Text(
                          days == 1 ? '1 day to go' : '$days days to go',
                          style: AppText.headline.copyWith(color: AppColors.primary),
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(height: AppSpacing.lg),
                if (eligible)
                  FilledButton(
                    onPressed: () => context.push('/appointments/book'),
                    child: const Text('Book a donation'),
                  )
                else if (illness)
                  FilledButton(
                    onPressed: () => context.push('/donor-profile'),
                    child: const Text('Update my health details'),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}
