import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api_client.dart';
import '../../../core/date_format.dart';
import '../../../theme/app_theme.dart';
import '../../../theme/tokens.dart';
import '../../auth/auth_controller.dart';
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
    'recent_surgery': 'Recent surgery',
    'chronic_condition': 'Chronic condition',
    'hiv_positive': 'HIV positive',
    'hepatitis': 'Hepatitis',
  };

  @override
  void dispose() {
    _address.dispose();
    super.dispose();
  }

  Future<void> _save(String id, Map<String, bool> medicalFlags) async {
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
      firstDate: DateTime(1900),
      lastDate: DateTime.now(),
    );
    if (picked != null) setState(() => _lastDonationDate = picked);
  }

  @override
  Widget build(BuildContext context) {
    final profileAsync = ref.watch(donorProfileProvider);
    final eligibility = ref.watch(eligibilityProvider).valueOrNull;
    return Scaffold(
      appBar: AppBar(title: const Text('Donor profile')),
      body: profileAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorState(onRetry: () => ref.invalidate(donorProfileProvider)),
        data: (profile) {
          if (profile == null) {
            return Center(child: FilledButton(onPressed: () => context.go('/donor-registration'), child: const Text('Register as a donor')));
          }
          if (_loadedProfileId != profile.id) {
            _loadedProfileId = profile.id;
            _address.text = profile.address ?? '';
            _lastDonationDate = profile.lastDonationDate;
            _medicalFlags = Map<String, bool>.from(profile.medicalFlags);
          }
          final availability = eligibility?.isEligible == true
              ? 'Eligible to donate now'
              : eligibility?.daysUntilEligible != null && eligibility!.daysUntilEligible! > 0
                  ? '${eligibility.daysUntilEligible} days until you can donate again'
                  : 'Not currently eligible';
          final activeMedicalFlags = profile.medicalFlags.entries
              .where((entry) => entry.value)
              .map((entry) => _medicalFlagLabels[entry.key] ?? entry.key)
              .toList();
          return ListView(
            padding: const EdgeInsets.all(AppSpacing.gutter),
            children: [
              Text(profile.fullName.isEmpty ? 'Your donor profile' : profile.fullName, style: AppText.title),
              const SizedBox(height: AppSpacing.md),
              Row(children: [
                _Badge(label: profile.bloodType, color: AppColors.primary),
                const SizedBox(width: AppSpacing.sm),
                _Badge(label: profile.verifiedByAdmin ? 'Verified' : 'Pending verification', color: profile.verifiedByAdmin ? AppColors.success : AppColors.scheduled),
              ]),
              const SizedBox(height: AppSpacing.xl),
              _DetailsCard(
                title: 'Donation availability',
                children: [
                  _DetailRow(label: 'Eligibility', value: availability),
                  _DetailRow(label: 'Last donation', value: _formatDate(profile.lastDonationDate)),
                  _DetailRow(label: 'Date of birth', value: _formatDate(profile.dateOfBirth)),
                ],
              ),
              const SizedBox(height: AppSpacing.md),
              _DetailsCard(
                title: 'Location and health',
                children: [
                  _DetailRow(label: 'Address', value: profile.address?.isNotEmpty == true ? profile.address! : 'Not provided'),
                  _DetailRow(
                    label: 'Coordinates',
                    value: profile.locationVerified && profile.latitude != null && profile.longitude != null
                        ? '${profile.latitude!.toStringAsFixed(4)}, ${profile.longitude!.toStringAsFixed(4)}'
                        : 'Location not verified',
                  ),
                  _DetailRow(
                    label: 'Illness or medical cases',
                    value: activeMedicalFlags.isEmpty ? 'None reported' : activeMedicalFlags.join(', '),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.lg),
              TextFormField(controller: _address, maxLines: 2, decoration: const InputDecoration(labelText: 'Address')),
              const SizedBox(height: AppSpacing.md),
              Text('Illness and medical cases', style: AppText.bodyStrong),
              const SizedBox(height: AppSpacing.xs),
              for (final entry in _medicalFlagLabels.entries)
                CheckboxListTile(
                  contentPadding: EdgeInsets.zero,
                  dense: true,
                  title: Text(entry.value),
                  value: _medicalFlags[entry.key] ?? false,
                  onChanged: _saving ? null : (value) => setState(() => _medicalFlags = {..._medicalFlags, entry.key: value ?? false}),
                ),
              const SizedBox(height: AppSpacing.xs),
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Last donation date'),
                subtitle: Text(_lastDonationDate == null ? 'Not recorded' : isoDate(_lastDonationDate!)),
                trailing: const Icon(Icons.calendar_today_outlined),
                onTap: _saving ? null : _pickDonationDate,
              ),
              if (_error != null) Text(_error!, style: AppText.bodySmall.copyWith(color: AppColors.critical)),
              const SizedBox(height: AppSpacing.lg),
              FilledButton(onPressed: _saving ? null : () => _save(profile.id, profile.medicalFlags), child: _saving ? const CircularProgressIndicator() : const Text('Save changes')),
              const SizedBox(height: AppSpacing.sm),
              OutlinedButton(onPressed: () => context.go('/eligibility'), child: const Text('View eligibility details')),
            ],
          );
        },
      ),
    );
  }
}

class _Badge extends StatelessWidget {
  const _Badge({required this.label, required this.color});
  final String label;
  final Color color;
  @override
  Widget build(BuildContext context) => Chip(label: Text(label), backgroundColor: color.withValues(alpha: .12), side: BorderSide(color: color));
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.onRetry});
  final VoidCallback onRetry;
  @override
  Widget build(BuildContext context) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
        const Text('Could not load your profile.'),
        TextButton(onPressed: onRetry, child: const Text('Retry')),
      ]));
}

String _formatDate(DateTime? date) {
  if (date == null) return 'Not recorded';
  return isoDate(date);
}

class _DetailsCard extends StatelessWidget {
  const _DetailsCard({required this.title, required this.children});
  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: AppText.bodyStrong),
              const SizedBox(height: AppSpacing.xs),
              ...children,
            ],
          ),
        ),
      );
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: AppSpacing.xs),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: Text(label, style: AppText.bodySmall)),
            const SizedBox(width: AppSpacing.sm),
            Flexible(child: Text(value, style: AppText.bodyStrong, textAlign: TextAlign.end)),
          ],
        ),
      );
}