import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/avatar.dart';
import '../../widgets/states.dart';
import '../auth/auth_controller.dart';
import '../donor_profile/eligibility_text.dart';
import '../donor_profile/providers/donor_profile_provider.dart';

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authControllerProvider);
    final isDonor = (auth.role ?? 'donor') == 'donor';

    return Scaffold(
      backgroundColor: AppColors.canvas,
      body: SafeArea(
        bottom: false,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.xs, AppSpacing.gutter, AppSpacing.xl),
          children: [
            Row(
              children: [
                InitialsAvatar(initials: auth.initials, size: 56),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        (auth.fullName?.isNotEmpty ?? false) ? auth.fullName! : (auth.firstName ?? 'Your profile'),
                        style: AppText.title,
                      ),
                      if (auth.email != null)
                        Text(auth.email!, style: AppText.bodySmall),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.xl),
            const SectionLabel('Account'),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  _InfoRow(icon: Icons.badge_outlined, label: 'Role', value: _titleCase(auth.role ?? 'donor')),
                  const Divider(height: 1, indent: AppSpacing.md, endIndent: AppSpacing.md),
                  _InfoRow(icon: Icons.mail_outline, label: 'Email', value: auth.email ?? '—'),
                ],
              ),
            ),
            if (isDonor) ...[
              const SizedBox(height: AppSpacing.xl),
              const SectionLabel('Donor'),
              AppCard(
                padding: EdgeInsets.zero,
                child: Column(
                  children: [
                    _LinkRow(
                      icon: Icons.badge_outlined,
                      title: 'Donor profile',
                      subtitle: 'Blood type, address and health details',
                      onTap: () => context.push('/donor-profile'),
                    ),
                    const Divider(height: 1, indent: AppSpacing.md + 32 + AppSpacing.sm),
                    _LinkRow(
                      icon: Icons.verified_outlined,
                      title: 'My eligibility',
                      subtitle: eligibilitySummary(ref.watch(eligibilityProvider).valueOrNull),
                      onTap: () => context.push('/eligibility'),
                    ),
                    const Divider(height: 1, indent: AppSpacing.md + 32 + AppSpacing.sm),
                    _LinkRow(
                      icon: Icons.notifications_none,
                      title: 'Notifications',
                      subtitle: 'Requests and appointment updates',
                      onTap: () => context.push('/notifications'),
                    ),
                  ],
                ),
              ),
            ],
            const SizedBox(height: AppSpacing.xl),
            const SectionLabel('About'),
            AppCard(
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(Icons.water_drop_outlined, size: 18, color: AppColors.primary),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      isDonor
                          ? 'When a hospital near you needs your blood type, PulsePoint’s '
                              'agents can match you to the request. A staff member approves '
                              'every match before you are contacted, and you confirm or '
                              'decline the appointment here.'
                          : 'Raise blood requests for your organization and follow each '
                              'one through the coordinator workflow: stock check, donor '
                              'matching, eligibility and your approval.',
                      style: AppText.bodySmall,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.xl),
            AppCard(
              padding: EdgeInsets.zero,
              child: Material(
                color: Colors.transparent,
                child: InkWell(
                  onTap: () => _confirmSignOut(context, ref),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm, horizontal: AppSpacing.md),
                    child: Center(
                      child: Text('Sign out', style: AppText.bodyStrong.copyWith(color: AppColors.critical)),
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  String _titleCase(String s) => s.isEmpty ? s : '${s[0].toUpperCase()}${s.substring(1)}';

  Future<void> _confirmSignOut(BuildContext context, WidgetRef ref) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Sign out?'),
        content: const Text('You can sign back in any time.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Sign out')),
        ],
      ),
    );
    if (ok == true) {
      await ref.read(authControllerProvider.notifier).logout();
    }
  }
}

class _LinkRow extends StatelessWidget {
  const _LinkRow({required this.icon, required this.title, required this.subtitle, required this.onTap});
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
          padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.sm),
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
              const Icon(Icons.chevron_right, size: 20, color: AppColors.inkFaint),
            ],
          ),
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.icon, required this.label, required this.value});
  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Row(
        children: [
          Icon(icon, size: 18, color: AppColors.inkMuted),
          const SizedBox(width: AppSpacing.sm),
          Text(label, style: AppText.bodySmall),
          const Spacer(),
          Text(value, style: AppText.bodyStrong),
        ],
      ),
    );
  }
}
