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
  bool _saving = false;
  String? _error;

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
        'medicalFlags': medicalFlags,
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
    return Scaffold(
      appBar: AppBar(title: const Text('Donor profile')),
      body: profileAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorState(onRetry: () => ref.invalidate(donorProfileProvider)),
        data: (profile) {
          if (profile == null) {
            return Center(child: FilledButton(onPressed: () => context.go('/donor-registration'), child: const Text('Register as a donor')));
          }
          if (_address.text.isEmpty) _address.text = profile.address ?? '';
          _lastDonationDate ??= profile.lastDonationDate;
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
              TextFormField(controller: _address, maxLines: 2, decoration: const InputDecoration(labelText: 'Address')),
              const SizedBox(height: AppSpacing.md),
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
              OutlinedButton(onPressed: () => context.go('/eligibility'), child: const Text('Check eligibility')),
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