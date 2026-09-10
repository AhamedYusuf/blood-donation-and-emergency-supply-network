import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/date_format.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/states.dart';
import 'appointment.dart';
import 'appointments_repository.dart';

class BookAppointmentScreen extends ConsumerStatefulWidget {
  const BookAppointmentScreen({super.key});

  @override
  ConsumerState<BookAppointmentScreen> createState() => _BookAppointmentScreenState();
}

class _BookAppointmentScreenState extends ConsumerState<BookAppointmentScreen> {
  BloodBank? _bank;
  DateTime? _date;
  TimeOfDay? _time;
  bool _submitting = false;
  String? _error;

  DateTime? get _slot => (_date != null && _time != null)
      ? DateTime(_date!.year, _date!.month, _date!.day, _time!.hour, _time!.minute)
      : null;

  Future<void> _pickBank() async {
    final banks = ref.read(bloodBanksProvider).valueOrNull ?? const <BloodBank>[];
    final picked = await showModalBottomSheet<BloodBank>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _BankPicker(banks: banks, selectedId: _bank?.id),
    );
    if (picked != null) setState(() => _bank = picked);
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final d = await showDatePicker(
      context: context,
      initialDate: _date ?? now.add(const Duration(days: 1)),
      firstDate: now,
      lastDate: now.add(const Duration(days: 90)),
    );
    if (d != null) setState(() => _date = d);
  }

  Future<void> _pickTime() async {
    final t = await showTimePicker(
      context: context,
      initialTime: _time ?? const TimeOfDay(hour: 9, minute: 0),
    );
    if (t != null) setState(() => _time = t);
  }

  Future<void> _submit() async {
    setState(() => _error = null);
    if (_bank == null) {
      setState(() => _error = 'Choose a blood bank.');
      return;
    }
    final slot = _slot;
    if (slot == null || !slot.isAfter(DateTime.now())) {
      setState(() => _error = 'Pick a date and time in the future.');
      return;
    }

    setState(() => _submitting = true);
    try {
      await ref.read(appointmentsRepositoryProvider)
          .book(organizationId: _bank!.id, scheduledTime: slot);
      if (mounted) Navigator.of(context).pop(true);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } catch (_) {
      setState(() => _error = 'Something went wrong. Please try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final banksAsync = ref.watch(bloodBanksProvider);
    final slot = _slot;

    return Scaffold(
      appBar: AppBar(title: Text('Book a donation', style: AppText.title)),
      body: banksAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, _) => ErrorState(
          message: 'We couldn’t load the list of blood banks.',
          onRetry: () => ref.invalidate(bloodBanksProvider),
        ),
        data: (_) => ListView(
          padding: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.md, AppSpacing.gutter, AppSpacing.md),
          children: [
            const SectionLabel('Where'),
            AppCard(
              onTap: _pickBank,
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Row(
                children: [
                  const Icon(Icons.local_hospital_outlined, size: 20, color: AppColors.inkMuted),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: _bank == null
                        ? Text('Choose a blood bank', style: AppText.body)
                        : Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(_bank!.name, style: AppText.bodyStrong),
                              if (_bank!.address.isNotEmpty)
                                Text(_bank!.address, style: AppText.caption),
                            ],
                          ),
                  ),
                  const Icon(Icons.chevron_right, color: AppColors.inkFaint),
                ],
              ),
            ),

            const SizedBox(height: AppSpacing.lg),
            const SectionLabel('When'),
            Row(
              children: [
                Expanded(
                  child: _PickerField(
                    icon: Icons.calendar_today_outlined,
                    label: 'Date',
                    value: _date == null
                        ? null
                        : '${weekdayAbbr(_date!)} ${_date!.day} ${monthAbbr(_date!)}',
                    onTap: _pickDate,
                  ),
                ),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: _PickerField(
                    icon: Icons.schedule_outlined,
                    label: 'Time',
                    value: _time?.format(context),
                    onTap: _pickTime,
                  ),
                ),
              ],
            ),

            if (_bank != null && slot != null) ...[
              const SizedBox(height: AppSpacing.lg),
              _SummaryCard(bank: _bank!.name, slot: slot),
            ],

            if (_error != null) ...[
              const SizedBox(height: AppSpacing.md),
              _ErrorLine(_error!),
            ],
          ],
        ),
      ),
      bottomNavigationBar: _BottomBar(
        enabled: !_submitting,
        submitting: _submitting,
        onSubmit: _submit,
      ),
    );
  }
}

class _PickerField extends StatelessWidget {
  const _PickerField({required this.icon, required this.label, required this.value, required this.onTap});
  final IconData icon;
  final String label;
  final String? value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      onTap: onTap,
      padding: const EdgeInsets.symmetric(horizontal: AppSpacing.sm, vertical: AppSpacing.sm),
      child: Row(
        children: [
          Icon(icon, size: 18, color: AppColors.inkMuted),
          const SizedBox(width: AppSpacing.xs),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: AppText.caption),
                const SizedBox(height: 1),
                Text(value ?? 'Select',
                    style: value == null
                        ? AppText.bodySmall.copyWith(color: AppColors.inkFaint)
                        : AppText.bodyStrong),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  const _SummaryCard({required this.bank, required this.slot});
  final String bank;
  final DateTime slot;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.md),
      decoration: BoxDecoration(
        color: AppColors.primarySubtle,
        borderRadius: BorderRadius.circular(AppRadii.md),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(Icons.check_circle_outline, size: 18, color: AppColors.primary),
          const SizedBox(width: AppSpacing.xs),
          Expanded(
            child: RichText(
              text: TextSpan(
                style: AppText.bodySmall.copyWith(color: AppColors.inkSecondary),
                children: [
                  const TextSpan(text: 'Donating at '),
                  TextSpan(text: bank, style: AppText.bodySmall.copyWith(color: AppColors.ink, fontWeight: FontWeight.w600)),
                  const TextSpan(text: '\n'),
                  TextSpan(text: longStamp(slot), style: AppText.bodySmall.copyWith(color: AppColors.ink, fontWeight: FontWeight.w600)),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _ErrorLine extends StatelessWidget {
  const _ErrorLine(this.message);
  final String message;
  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        const Icon(Icons.error_outline, size: 16, color: AppColors.critical),
        const SizedBox(width: AppSpacing.xs),
        Expanded(child: Text(message, style: AppText.caption.copyWith(color: AppColors.critical, letterSpacing: 0))),
      ],
    );
  }
}

class _BottomBar extends StatelessWidget {
  const _BottomBar({required this.enabled, required this.submitting, required this.onSubmit});
  final bool enabled;
  final bool submitting;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: AppColors.hairline)),
      ),
      child: SafeArea(
        minimum: const EdgeInsets.fromLTRB(AppSpacing.gutter, AppSpacing.sm, AppSpacing.gutter, AppSpacing.sm),
        child: FilledButton(
          onPressed: enabled ? onSubmit : null,
          child: submitting
              ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: AppColors.onPrimary))
              : const Text('Confirm booking'),
        ),
      ),
    );
  }
}

class _BankPicker extends StatelessWidget {
  const _BankPicker({required this.banks, required this.selectedId});
  final List<BloodBank> banks;
  final String? selectedId;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(AppSpacing.lg, 0, AppSpacing.lg, AppSpacing.md),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.xs),
              child: Text('Choose a blood bank', style: AppText.headline),
            ),
            ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: 360),
              child: ListView.separated(
                shrinkWrap: true,
                itemCount: banks.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (_, i) {
                  final b = banks[i];
                  final selected = b.id == selectedId;
                  return ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(b.name, style: AppText.bodyStrong),
                    subtitle: b.address.isEmpty ? null : Text(b.address, style: AppText.caption),
                    trailing: selected
                        ? const Icon(Icons.check, color: AppColors.primary)
                        : null,
                    onTap: () => Navigator.pop(context, b),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}
