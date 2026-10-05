import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api_client.dart';
import '../../../core/date_format.dart';
import '../../../theme/app_theme.dart';
import '../../../theme/tokens.dart';
import '../../../widgets/app_card.dart';
import '../../../widgets/states.dart';
import '../../auth/auth_controller.dart';
import '../eligibility_text.dart';
import '../providers/donor_profile_provider.dart';

class DonorProfileScreen extends ConsumerStatefulWidget {
  const DonorProfileScreen({super.key});

  @override
  ConsumerState<DonorProfileScreen> createState() => _DonorProfileScreenState();
}

class _DonorProfileScreenState extends ConsumerState<DonorProfileScreen> {
  final _address = TextEditingController();
  DateTime? _lastDonationDate;
  Map<String, bool> _medicalFlags = {};
  String? _loadedProfileId;
  bool _saving = false;
  String? _error;

  static const _medicalFlagLabels = <String, String>{
    'recent_illness': 'Recent illness or infection',
    'recent_surgery': 'Recent surgery (within 6 months)',
    'chronic_condition': 'Chronic condition',
    'hiv_positive': 'HIV positive',
    'hepatitis': 'Hepatitis B or C',
  };

  @override
  void dispose() {
    _address.dispose();
    super.dispose();
  }

  Future<void> _save(String id) async {
    final token = ref.read(authTokenProvider);
    if (token == null) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ref.read(donorRepositoryProvider).update(id, {
        'address': _address.text.trim(),
        'medicalFlags': _medicalFlags,
        'lastDonationDate': _lastDonationDate == null ? null : isoDate(_lastDonationDate!),
      }, token: token);
      ref.invalidate(donorProfileProvider);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Your donor profile was saved.')),
        );
      }
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _pickDonationDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _lastDonationDate ?? DateTime.now(),
      firstDate: DateTime(1950),
      lastDate: DateTime.now(),
    );
    if (picked != null) setState(() => _lastDonationDate = picked);
  }

  @override
  Widget build(BuildContext context) {
    final profileAsync = ref.watch(donorProfileProvider);
    final eligibility = ref.watch(eligibilityProvider).valueOrNull;
    return Scaffold(
      backgroundColor: AppColors.canvas,
      appBar: AppBar(title: const Text('Donor profile')),
      body: profileAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, _) => ErrorState(
          message: 'We couldn’t load your donor profile.',
          onRetry: () => ref.invalidate(donorProfileProvider),
        ),
        data: (profile) {
          if (profile == null) {
            return EmptyState(
              icon: Icons.volunteer_activism_outlined,
              title: 'No donor profile yet',
              message: 'Register as a donor to book donations and see requests near you.',
              action: FilledButton(
                onPressed: () => context.go('/donor-registration'),
                child: const Text('Register as a donor'),
              ),
            );
          }
          if (_loadedProfileId != profile.id) {
            _loadedProfileId = profile.id;
            _address.text = profile.address ?? '';
            _lastDonationDate = profile.lastDonationDate;
            _medicalFlags = {
              for (final key in _medicalFlagLabels.keys) key: profile.medicalFlags[key] ?? false,
            };
          }
          return ListView(
            padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.md, AppSpacing.gutter, AppSpacing.xl),
            children: [
              Text(profile.fullName.isEmpty ? 'Your donor profile' : profile.fullName, style: AppText.title),
              const SizedBox(height: AppSpacing.sm),
              Wrap(spacing: AppSpacing.xs, runSpacing: AppSpacing.xs, children: [
                _Pill(label: 'Blood type ${profile.bloodType}', color: AppColors.primary),
                _Pill(
                  label: profile.verifiedByAdmin ? 'Verified donor' : 'Waiting for verification',
                  color: profile.verifiedByAdmin ? AppColors.success : AppColors.scheduled,
                ),
              ]),
              const SizedBox(height: AppSpacing.lg),
              const SectionLabel('Donation availability'),
              AppCard(
                padding: EdgeInsets.zero,
                onTap: () => context.push('/eligibility'),
                child: Column(children: [
                  _Row(label: 'Status', value: eligibilitySummary(eligibility), chevron: true),
                  const Divider(height: 1, indent: AppSpacing.md, endIndent: AppSpacing.md),
                  _Row(label: 'Last donation', value: _formatDate(profile.lastDonationDate)),
                  const Divider(height: 1, indent: AppSpacing.md, endIndent: AppSpacing.md),
                  _Row(label: 'Date of birth', value: _formatDate(profile.dateOfBirth)),
                ]),
              ),
              const SizedBox(height: AppSpacing.lg),
              const SectionLabel('Location'),
              AppCard(
                padding: EdgeInsets.zero,
                child: Column(children: [
                  _Row(label: 'Address', value: profile.address?.isNotEmpty == true ? profile.address! : 'Not provided'),
                  const Divider(height: 1, indent: AppSpacing.md, endIndent: AppSpacing.md),
                  _Row(
                    label: 'Location',
                    value: profile.locationVerified && profile.latitude != null && profile.longitude != null
                        ? 'Found on the map'
                        : 'Not found yet',
                  ),
                ]),
              ),
              const SizedBox(height: AppSpacing.xl),
              const SectionLabel('Update your details'),
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(
                      controller: _address,
                      maxLines: 2,
                      decoration: const InputDecoration(labelText: 'Address'),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    InkWell(
                      onTap: _saving ? null : _pickDonationDate,
                      child: InputDecorator(
                        decoration: const InputDecoration(
                          labelText: 'Last donation date',
                          suffixIcon: Icon(Icons.calendar_today_outlined),
                        ),
                        child: Text(_lastDonationDate == null ? 'Not recorded' : isoDate(_lastDonationDate!)),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    Text('Health', style: AppText.bodyStrong),
                    Text('These help us keep every donation safe.', style: AppText.caption),
                    for (final entry in _medicalFlagLabels.entries)
                      SwitchListTile(
                        contentPadding: EdgeInsets.zero,
                        title: Text(entry.value, style: AppText.body),
                        value: _medicalFlags[entry.key] ?? false,
                        onChanged: _saving
                            ? null
                            : (value) => setState(() => _medicalFlags = {..._medicalFlags, entry.key: value}),
                      ),
                  ],
                ),
              ),
              if (_error != null) ...[
                const SizedBox(height: AppSpacing.sm),
                Text(_error!, style: AppText.bodySmall.copyWith(color: AppColors.critical)),
              ],
              const SizedBox(height: AppSpacing.lg),
              FilledButton(
                onPressed: _saving ? null : () => _save(profile.id),
                child: _saving
                    ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : const Text('Save changes'),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.label, required this.color});
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.sm, vertical: 4),
        decoration: BoxDecoration(
          color: color.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(999),
        ),
        child: Text(label, style: AppText.caption.copyWith(color: color, fontWeight: FontWeight.w600)),
      );
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.value, this.chevron = false});
  final String label;
  final String value;
  final bool chevron;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.md, vertical: AppSpacing.sm),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: AppText.bodySmall),
            const SizedBox(width: AppSpacing.sm),
            Expanded(child: Text(value, style: AppText.bodyStrong, textAlign: TextAlign.end)),
            if (chevron) ...[
              const SizedBox(width: AppSpacing.xxs),
              const Icon(Icons.chevron_right, size: 20, color: AppColors.inkFaint),
            ],
          ],
        ),
      );
}

String _formatDate(DateTime? date) => date == null ? 'Not recorded' : isoDate(date);
