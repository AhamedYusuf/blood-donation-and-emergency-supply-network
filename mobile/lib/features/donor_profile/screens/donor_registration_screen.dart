import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/age.dart';
import '../../../core/api_client.dart';
import '../../../core/date_format.dart';
import '../../../theme/app_theme.dart';
import '../../../theme/tokens.dart';
import '../../auth/auth_controller.dart';
import '../providers/donor_profile_provider.dart';

class DonorRegistrationScreen extends ConsumerStatefulWidget {
  const DonorRegistrationScreen({super.key});

  @override
  ConsumerState<DonorRegistrationScreen> createState() => _DonorRegistrationScreenState();
}

// Mirrors the web app's DonorProfilePage.tsx exactly. Two things used
// to be out of sync with web badly enough to matter:
//  - Minimum age was 16 here vs 18 on web — a real eligibility-rule
//    mismatch, not cosmetic (a 16/17-year-old could get a donor profile
//    via mobile that web's own registration would have refused).
//  - Only one of the five medical screening flags (recent illness) was
//    ever collected — a donor with e.g. HIV-positive or hepatitis status
//    set on web had no mobile-side equivalent question at all, meaning
//    a mobile-only registration could silently skip screening questions
//    the web flow always asks.
// Keys MUST match what EligibilityRuleEngine.cs looks up via
// TryGetValue — do not rename these (same constraint as the web copy).
const Map<String, String> _medicalFlagLabels = {
  'recent_illness': 'Recent illness or infection',
  'recent_surgery': 'Recent surgery (within 6 months)',
  'chronic_condition': 'Chronic condition (e.g. diabetes, heart disease)',
  'hiv_positive': 'HIV positive',
  'hepatitis': 'Hepatitis B or C',
};

class _DonorRegistrationScreenState extends ConsumerState<DonorRegistrationScreen> {
  final _formKey = GlobalKey<FormState>();
  final _address = TextEditingController();
  String? _bloodType;
  DateTime? _dateOfBirth;
  bool _hasDonatedBefore = false;
  DateTime? _lastDonationDate;
  final Map<String, bool> _medicalFlags = {
    for (final key in _medicalFlagLabels.keys) key: false,
  };
  bool _submitting = false;
  String? _error;

  static const _bloodTypes = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];

  @override
  void dispose() {
    _address.dispose();
    super.dispose();
  }

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime(now.year - 18, now.month, now.day),
      firstDate: DateTime(now.year - 100, now.month, now.day),
      lastDate: now,
    );
    if (picked != null) setState(() => _dateOfBirth = picked);
  }

  Future<void> _pickLastDonationDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _lastDonationDate ?? now,
      firstDate: DateTime(now.year - 5),
      lastDate: now,
    );
    if (picked != null) setState(() => _lastDonationDate = picked);
  }

  bool get _dobValid => _dateOfBirth != null && isAtLeastYearsOld(_dateOfBirth!, 18);
  bool get _lastDonationValid => !_hasDonatedBefore || _lastDonationDate != null;

  Future<void> _submit() async {
    final formValid = _formKey.currentState!.validate();
    if (!formValid || !_dobValid || !_lastDonationValid) {
      setState(() {});
      return;
    }
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      final token = ref.read(authTokenProvider);
      if (token == null) throw ApiException('Your session has expired. Please sign in again.');
      final profile = await ref.read(donorRepositoryProvider).register({
            'bloodType': _bloodType,
            'dateOfBirth': isoDate(_dateOfBirth!),
            'address': _address.text.trim(),
            if (_hasDonatedBefore && _lastDonationDate != null)
              'lastDonationDate': isoDate(_lastDonationDate!),
            'medicalFlags': _medicalFlags,
          }, token: token);
      await ref.read(authControllerProvider.notifier).setDonorProfileId(profile.id);
      if (mounted) context.go('/donor-profile');
    } on ApiException catch (error) {
      // The backend (DonorsController.Register) returns 400 with a clear,
      // actionable message for both "already registered" and "address
      // couldn't be geocoded" — ApiClient now surfaces it correctly, so
      // there's no need to override it with different wording here.
      setState(() => _error = error.message);
    } catch (_) {
      setState(() => _error = 'Could not connect. Check your connection and try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Donor registration')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.gutter),
          children: [
            Text('Tell us about yourself', style: AppText.title),
            const SizedBox(height: AppSpacing.xs),
            Text('This information helps us keep donations safe and useful.', style: AppText.bodySmall),
            const SizedBox(height: AppSpacing.xl),
            DropdownButtonFormField<String>(
              initialValue: _bloodType,
              decoration: const InputDecoration(labelText: 'Blood type'),
              items: _bloodTypes.map((type) => DropdownMenuItem(value: type, child: Text(type))).toList(),
              onChanged: _submitting ? null : (value) => setState(() => _bloodType = value),
              validator: (value) => value == null ? 'Select your blood type' : null,
            ),
            const SizedBox(height: AppSpacing.md),
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Date of birth'),
              subtitle: Text(_dateOfBirth == null ? 'Select your date of birth' : isoDate(_dateOfBirth!)),
              trailing: const Icon(Icons.calendar_today_outlined),
              onTap: _submitting ? null : _pickDateOfBirth,
            ),
            if (_dateOfBirth == null)
              Text('Date of birth is required', style: AppText.bodySmall.copyWith(color: AppColors.critical)),
            if (_dateOfBirth != null && !isAtLeastYearsOld(_dateOfBirth!, 18))
              Text('You must be at least 18 years old to register',
                  style: AppText.bodySmall.copyWith(color: AppColors.critical)),
            const SizedBox(height: AppSpacing.md),
            TextFormField(
              controller: _address,
              maxLines: 2,
              decoration: const InputDecoration(labelText: 'Address', hintText: 'Colombo, Sri Lanka'),
              validator: (value) => value == null || value.trim().isEmpty ? 'Enter your address' : null,
            ),
            const SizedBox(height: AppSpacing.md),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Have you donated blood before?'),
              value: _hasDonatedBefore,
              onChanged: _submitting
                  ? null
                  : (value) => setState(() {
                        _hasDonatedBefore = value;
                        if (!value) _lastDonationDate = null;
                      }),
            ),
            if (_hasDonatedBefore) ...[
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Last donation date'),
                subtitle: Text(_lastDonationDate == null
                    ? 'Select the date'
                    : isoDate(_lastDonationDate!)),
                trailing: const Icon(Icons.calendar_today_outlined),
                onTap: _submitting ? null : _pickLastDonationDate,
              ),
              if (_lastDonationDate == null)
                Text('Last donation date is required',
                    style: AppText.bodySmall.copyWith(color: AppColors.critical)),
            ],
            const SizedBox(height: AppSpacing.md),
            Text('Medical screening', style: AppText.label),
            const SizedBox(height: AppSpacing.xxs),
            Text(
              'This helps us keep donations safe — answer honestly.',
              style: AppText.bodySmall.copyWith(color: AppColors.inkMuted),
            ),
            for (final entry in _medicalFlagLabels.entries)
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(entry.value),
                value: _medicalFlags[entry.key] ?? false,
                onChanged: _submitting
                    ? null
                    : (value) => setState(() => _medicalFlags[entry.key] = value),
              ),
            if (_error != null) ...[
              const SizedBox(height: AppSpacing.md),
              Text(_error!, style: AppText.bodySmall.copyWith(color: AppColors.critical)),
            ],
            const SizedBox(height: AppSpacing.lg),
            FilledButton(
              onPressed: _submitting ? null : _submit,
              child: _submitting
                  ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Register as a donor'),
            ),
          ],
        ),
      ),
    );
  }

}