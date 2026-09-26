import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter/services.dart';

import '../../core/api_client.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/states.dart';
import '../auth/auth_controller.dart';
import 'organization.dart';
import 'blood_requests_repository.dart';
import 'organizations_repository.dart';

const _bloodTypes = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];
const _urgencies = ['normal', 'urgent', 'critical'];

final _integerInputFormatter = TextInputFormatter.withFunction(
  (oldValue, newValue) =>
      RegExp(r'^\d*$').hasMatch(newValue.text) ? newValue : oldValue,
);

final _coordinateInputFormatter = TextInputFormatter.withFunction(
  (oldValue, newValue) =>
      RegExp(r'^-?(?:\d+)?(?:\.\d*)?$').hasMatch(newValue.text)
      ? newValue
      : oldValue,
);

class CreateBloodRequestScreen extends ConsumerStatefulWidget {
  const CreateBloodRequestScreen({super.key});

  @override
  ConsumerState<CreateBloodRequestScreen> createState() =>
      _CreateBloodRequestScreenState();
}

class _CreateBloodRequestScreenState
    extends ConsumerState<CreateBloodRequestScreen> {
  final _formKey = GlobalKey<FormState>();
  final _unitsController = TextEditingController();
  final _hospitalController = TextEditingController();
  final _latitudeController = TextEditingController();
  final _longitudeController = TextEditingController();
  final _notesController = TextEditingController();

  String? _bloodType;
  String? _urgency;
  String? _organizationId;
  String? _organizationError;
  bool _submitting = false;
  String? _error;

  @override
  void dispose() {
    _unitsController.dispose();
    _hospitalController.dispose();
    _latitudeController.dispose();
    _longitudeController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    final authState = ref.read(authControllerProvider);
    if (_organizationId == null || _organizationId!.isEmpty) {
      setState(
        () => _organizationError = authState.role == 'admin'
            ? 'Choose an organization'
            : 'Your account is not linked to an organization.',
      );
    }
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final organizationId = _organizationId!;

    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      await ref
          .read(bloodRequestsRepositoryProvider)
          .createRequest(
            organizationId: organizationId,
            bloodType: _bloodType!,
            unitsRequested: int.parse(_unitsController.text.trim()),
            urgency: _urgency!,
            hospitalName: _hospitalController.text.trim(),
            latitude: double.parse(_latitudeController.text.trim()),
            longitude: double.parse(_longitudeController.text.trim()),
            notes: _notesController.text.trim(),
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Blood request created')));
      Navigator.of(context).pop(true);
    } on ApiException catch (error) {
      setState(() => _error = error.message);
    } catch (_) {
      setState(() => _error = 'Something went wrong. Please try again.');
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authControllerProvider);
    final organizationsAsync = ref.watch(bloodRequestOrganizationsProvider);
    final organizations =
        organizationsAsync.valueOrNull ?? const <Organization>[];

    ref.listen(bloodRequestOrganizationsProvider, (_, next) {
      next.whenData((availableOrganizations) {
        final currentAuth = ref.read(authControllerProvider);
        final selectedId = currentAuth.role == 'admin'
            ? _organizationId
            : currentAuth.organizationId;
        if (selectedId != null) {
          _selectOrganization(selectedId, availableOrganizations);
        }
      });
    });

    return Scaffold(
      appBar: AppBar(title: Text('New blood request', style: AppText.title)),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(
            AppSpacing.gutter,
            AppSpacing.md,
            AppSpacing.gutter,
            AppSpacing.lg,
          ),
          children: [
            const SectionLabel('Request details'),
            AppCard(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Column(
                children: [
                  if (authState.role == 'admin') ...[
                    DropdownButtonFormField<String>(
                      initialValue:
                          organizations.any(
                            (organization) =>
                                organization.id == _organizationId,
                          )
                          ? _organizationId
                          : null,
                      decoration: const InputDecoration(
                        labelText: 'Organization',
                      ),
                      items: organizations
                          .map(
                            (organization) => DropdownMenuItem(
                              value: organization.id,
                              child: Text(organization.name),
                            ),
                          )
                          .toList(),
                      onChanged: _submitting || organizationsAsync.isLoading
                          ? null
                          : (value) =>
                                _selectOrganization(value, organizations),
                      validator: (_) => _organizationId == null
                          ? 'Choose an organization'
                          : _organizationError,
                    ),
                    if (organizationsAsync.hasError)
                      _organizationMessage('Unable to load organizations.'),
                    const SizedBox(height: AppSpacing.md),
                  ] else ...[
                    _StaffOrganization(
                      organization: organizations
                          .where(
                            (organization) =>
                                organization.id == _organizationId,
                          )
                          .firstOrNull,
                      organizationId: _organizationId,
                      error: _organizationError,
                    ),
                    const SizedBox(height: AppSpacing.md),
                  ],
                  DropdownButtonFormField<String>(
                    initialValue: _bloodType,
                    decoration: const InputDecoration(labelText: 'Blood type'),
                    items: _bloodTypes
                        .map(
                          (value) => DropdownMenuItem(
                            value: value,
                            child: Text(value),
                          ),
                        )
                        .toList(),
                    onChanged: _submitting
                        ? null
                        : (value) => setState(() => _bloodType = value),
                    validator: (value) =>
                        value == null ? 'Choose a blood type' : null,
                  ),
                  const SizedBox(height: AppSpacing.md),
                  TextFormField(
                    controller: _unitsController,
                    decoration: const InputDecoration(
                      labelText: 'Units requested',
                    ),
                    keyboardType: TextInputType.number,
                    inputFormatters: [_integerInputFormatter],
                    validator: _unitsValidator,
                  ),
                  const SizedBox(height: AppSpacing.md),
                  DropdownButtonFormField<String>(
                    initialValue: _urgency,
                    decoration: const InputDecoration(labelText: 'Urgency'),
                    items: _urgencies
                        .map(
                          (value) => DropdownMenuItem(
                            value: value,
                            child: Text(_titleCase(value)),
                          ),
                        )
                        .toList(),
                    onChanged: _submitting
                        ? null
                        : (value) => setState(() => _urgency = value),
                    validator: (value) =>
                        value == null ? 'Choose an urgency' : null,
                  ),
                  const SizedBox(height: AppSpacing.md),
                  TextFormField(
                    controller: _hospitalController,
                    decoration: const InputDecoration(
                      labelText: 'Hospital name',
                    ),
                    textCapitalization: TextCapitalization.words,
                    maxLength: 200,
                    validator: (value) {
                      final hospitalName = value?.trim() ?? '';
                      if (hospitalName.isEmpty) return 'Enter a hospital name';
                      if (hospitalName.length > 200) {
                        return 'Hospital name cannot exceed 200 characters';
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            const SectionLabel('Hospital location'),
            AppCard(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Row(
                children: [
                  Expanded(
                    child: _coordinateField(
                      _latitudeController,
                      'Latitude',
                      -90,
                      90,
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: _coordinateField(
                      _longitudeController,
                      'Longitude',
                      -180,
                      180,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            const SectionLabel('Notes'),
            AppCard(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: TextFormField(
                controller: _notesController,
                decoration: const InputDecoration(
                  labelText: 'Additional notes',
                ),
                maxLines: 4,
                maxLength: 1000,
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: AppSpacing.md),
              Text(
                _error!,
                style: AppText.bodySmall.copyWith(color: AppColors.critical),
              ),
            ],
            const SizedBox(height: AppSpacing.lg),
            FilledButton(
              onPressed: _submitting ? null : _submit,
              child: _submitting
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Create request'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _coordinateField(
    TextEditingController controller,
    String label,
    double min,
    double max,
  ) {
    return TextFormField(
      controller: controller,
      decoration: InputDecoration(labelText: label),
      readOnly: true,
      keyboardType: const TextInputType.numberWithOptions(
        decimal: true,
        signed: true,
      ),
      inputFormatters: [_coordinateInputFormatter],
      validator: (value) {
        final text = value?.trim() ?? '';
        if (text.isEmpty) return 'Enter $label';
        final parsed = double.tryParse(text);
        if (parsed == null) return 'Enter a valid $label';
        if (parsed < min || parsed > max) return '$min to $max';
        final otherController = identical(controller, _latitudeController)
            ? _longitudeController
            : _latitudeController;
        final otherCoordinate = double.tryParse(otherController.text.trim());
        if (parsed == 0 && otherCoordinate == 0) {
          return 'Enter a non-zero hospital location';
        }
        return null;
      },
    );
  }

  String? _unitsValidator(String? value) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) return 'Enter units requested';
    final units = int.tryParse(text);
    if (units == null) return 'Enter a whole number';
    if (units < 1 || units > 100) return 'Enter 1 to 100 units';
    return null;
  }

  void _selectOrganization(
    String? organizationId,
    List<Organization> organizations,
  ) {
    final selected = organizationId == null
        ? null
        : organizations
              .where((organization) => organization.id == organizationId)
              .firstOrNull;
    // isFinite alone treats (0, 0) as "valid" coordinates, but that's the
    // backend's own "no location set" convention (see the coordinate
    // field validator below, and CreateRequestDto's server-side check) —
    // without this, an organization with unset coordinates gets silently
    // auto-filled with "0"/"0" and shown as having no error here, only to
    // be rejected later by the field validator. Keep both checks agreed.
    final hasCoordinates =
        selected?.latitude?.isFinite == true &&
        selected?.longitude?.isFinite == true &&
        !(selected!.latitude == 0 && selected.longitude == 0);

    if (!mounted) return;
    setState(() {
      _organizationId = organizationId;
      _organizationError = organizationId == null
          ? 'Choose an organization'
          : hasCoordinates
          ? null
          : 'The selected organization does not have valid coordinates.';
      _latitudeController.text = hasCoordinates ? '${selected.latitude}' : '';
      _longitudeController.text = hasCoordinates
          ? '${selected.longitude}'
          : '';
    });
  }

  Widget _organizationMessage(String message) => Padding(
    padding: const EdgeInsets.only(top: AppSpacing.xs),
    child: Align(
      alignment: Alignment.centerLeft,
      child: Text(
        message,
        style: AppText.bodySmall.copyWith(color: AppColors.critical),
      ),
    ),
  );
}

class _StaffOrganization extends StatelessWidget {
  const _StaffOrganization({
    required this.organization,
    required this.organizationId,
    required this.error,
  });

  final Organization? organization;
  final String? organizationId;
  final String? error;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      InputDecorator(
        decoration: const InputDecoration(labelText: 'Organization'),
        child: Text(
          organization?.name ?? organizationId ?? 'Loading organization...',
        ),
      ),
      if (error != null)
        Padding(
          padding: const EdgeInsets.only(top: AppSpacing.xs),
          child: Text(
            error!,
            style: AppText.bodySmall.copyWith(color: AppColors.critical),
          ),
        ),
    ],
  );
}

String _titleCase(String value) =>
    value.isEmpty ? value : '${value[0].toUpperCase()}${value.substring(1)}';
