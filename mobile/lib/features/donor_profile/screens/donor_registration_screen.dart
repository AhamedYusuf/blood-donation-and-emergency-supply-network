import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

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

class _DonorRegistrationScreenState extends ConsumerState<DonorRegistrationScreen> {
  final _formKey = GlobalKey<FormState>();
  final _address = TextEditingController();
  String? _bloodType;
  DateTime? _dateOfBirth;
  bool _recentIllness = false;
  bool _submitting = false;
  String? _error;

  static const _bloodTypes = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];

  @override
  void dispose() {
    _address.dispose();
    super.dispose();
  }

  Future<void> _pickDateOfBirth() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime(DateTime.now().year - 18),
      firstDate: DateTime(1900),
      lastDate: DateTime.now(),
    );
    if (picked != null) setState(() => _dateOfBirth = picked);
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate() || _dateOfBirth == null) {
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
            'medicalFlags': {'recent_illness': _recentIllness},
          }, token: token);
      await ref.read(authControllerProvider.notifier).setDonorProfileId(profile.id);
      if (mounted) context.go('/donor-profile');
    } on ApiException catch (error) {
      setState(() => _error = error.statusCode == 409
          ? 'You already have a donor profile.'
          : error.message);
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
            if (_dateOfBirth != null && !_isAtLeast16(_dateOfBirth!))
              Text('You must be at least 16 years old', style: AppText.bodySmall.copyWith(color: AppColors.critical)),
            const SizedBox(height: AppSpacing.md),
            TextFormField(
              controller: _address,
              maxLines: 2,
              decoration: const InputDecoration(labelText: 'Address', hintText: 'Colombo, Sri Lanka'),
              validator: (value) => value == null || value.trim().isEmpty ? 'Enter your address' : null,
            ),
            const SizedBox(height: AppSpacing.sm),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Have you had a recent illness?'),
              value: _recentIllness,
              onChanged: _submitting ? null : (value) => setState(() => _recentIllness = value),
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

  bool _isAtLeast16(DateTime birthDate) {
    final today = DateTime.now();
    var age = today.year - birthDate.year;
    if (today.month < birthDate.month || (today.month == birthDate.month && today.day < birthDate.day)) age--;
    return age >= 16;
  }
}