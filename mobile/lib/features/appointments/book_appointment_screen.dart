import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import 'appointment.dart';
import 'appointments_repository.dart';

class BookAppointmentScreen extends ConsumerStatefulWidget {
  const BookAppointmentScreen({super.key});

  @override
  ConsumerState<BookAppointmentScreen> createState() =>
      _BookAppointmentScreenState();
}

class _BookAppointmentScreenState extends ConsumerState<BookAppointmentScreen> {
  BloodBank? _bank;
  DateTime? _slot;
  bool _submitting = false;
  String? _error;

  /// Device feature: native date + time selection.
  Future<void> _pickSlot() async {
    final now = DateTime.now();
    final date = await showDatePicker(
      context: context,
      initialDate: _slot ?? now.add(const Duration(days: 1)),
      firstDate: now,
      lastDate: now.add(const Duration(days: 90)),
    );
    if (date == null || !mounted) return;

    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(_slot ?? now),
    );
    if (time == null) return;

    setState(() {
      _slot = DateTime(date.year, date.month, date.day, time.hour, time.minute);
    });
  }

  Future<void> _submit() async {
    setState(() => _error = null);
    if (_bank == null) {
      setState(() => _error = 'Choose a blood bank.');
      return;
    }
    if (_slot == null || !_slot!.isAfter(DateTime.now())) {
      setState(() => _error = 'Pick a date and time in the future.');
      return;
    }

    setState(() => _submitting = true);
    try {
      await ref.read(appointmentsRepositoryProvider).book(
            organizationId: _bank!.id,
            scheduledTime: _slot!,
          );
      if (mounted) {
        Navigator.of(context).pop(true);
      }
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

    return Scaffold(
      appBar: AppBar(title: const Text('Book Appointment')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            banksAsync.when(
              loading: () => const LinearProgressIndicator(),
              error: (_, _) => const Text('Could not load blood banks.'),
              data: (banks) => DropdownButtonFormField<BloodBank>(
                initialValue: _bank,
                isExpanded: true,
                decoration: const InputDecoration(
                  labelText: 'Blood bank',
                  border: OutlineInputBorder(),
                ),
                items: [
                  for (final b in banks)
                    DropdownMenuItem(value: b, child: Text(b.name)),
                ],
                onChanged: (b) => setState(() => _bank = b),
              ),
            ),
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: _pickSlot,
              icon: const Icon(Icons.event),
              style: OutlinedButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 16),
                alignment: Alignment.centerLeft,
              ),
              label: Text(_slot == null ? 'Pick date & time' : _formatSlot(_slot!)),
            ),
            if (_error != null) ...[
              const SizedBox(height: 16),
              Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            const SizedBox(height: 24),
            FilledButton(
              onPressed: _submitting ? null : _submit,
              style: FilledButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
              child: _submitting
                  ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Confirm booking'),
            ),
          ],
        ),
      ),
    );
  }
}

String _formatSlot(DateTime t) =>
    '${t.day}/${t.month}/${t.year}  '
    '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';
