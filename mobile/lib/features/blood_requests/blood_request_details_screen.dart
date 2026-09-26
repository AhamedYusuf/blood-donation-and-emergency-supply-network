import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api_client.dart';
import '../../core/date_format.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/states.dart';
import '../../widgets/status_pill.dart';
import '../auth/auth_controller.dart';
import 'blood_request.dart';
import 'blood_request_ui.dart';
import 'blood_requests_repository.dart';

final bloodRequestProvider = FutureProvider.autoDispose
    .family<BloodRequest, String>((ref, id) {
      return ref.watch(bloodRequestsRepositoryProvider).getRequestById(id);
    });

const _requestStatuses = [
  'open',
  'matching',
  'awaiting_approval',
  'donors_notified',
  'partially_fulfilled',
  'fulfilled',
  'expired',
  'cancelled',
];

class BloodRequestDetailsScreen extends ConsumerStatefulWidget {
  const BloodRequestDetailsScreen({super.key, required this.requestId});

  final String requestId;

  @override
  ConsumerState<BloodRequestDetailsScreen> createState() =>
      _BloodRequestDetailsScreenState();
}

class _BloodRequestDetailsScreenState
    extends ConsumerState<BloodRequestDetailsScreen> {
  String? _selectedStatus;

  bool _updatingStatus = false;
  bool _closing = false;
  bool _deleting = false;

  Future<void> _refresh() async {
    ref.invalidate(bloodRequestProvider(widget.requestId));

    await ref.read(bloodRequestProvider(widget.requestId).future);
  }

  Future<void> _updateStatus(BloodRequest request) async {
    final selected = _selectedStatus;

    if (selected == null || selected == request.status.toLowerCase()) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Choose a different status first.')),
      );
      return;
    }

    setState(() {
      _updatingStatus = true;
    });

    try {
      await ref
          .read(bloodRequestsRepositoryProvider)
          .updateStatus(id: request.id, status: selected);

      ref.invalidate(bloodRequestProvider(widget.requestId));

      ref.invalidate(bloodRequestsProvider);

      if (!mounted) return;

      setState(() {
        _selectedStatus = null;
      });

      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Request status updated.')));
    } on ApiException catch (error) {
      if (!mounted) return;

      _showError(error.message);
    } catch (_) {
      if (!mounted) return;

      _showError('Could not update the request status.');
    } finally {
      if (mounted) {
        setState(() {
          _updatingStatus = false;
        });
      }
    }
  }

  Future<void> _closeRequest(BloodRequest request) async {
    final confirmed = await _confirm(
      title: 'Close request?',
      message:
          'This will mark the request as cancelled. '
          'You can still view its history.',
      confirmLabel: 'Close request',
    );

    if (confirmed != true || !mounted) {
      return;
    }

    setState(() {
      _closing = true;
    });

    try {
      await ref.read(bloodRequestsRepositoryProvider).closeRequest(request.id);

      ref.invalidate(bloodRequestProvider(widget.requestId));

      ref.invalidate(bloodRequestsProvider);

      if (!mounted) return;

      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Blood request closed.')));
    } on ApiException catch (error) {
      if (!mounted) return;

      _showError(error.message);
    } catch (_) {
      if (!mounted) return;

      _showError('Could not close the blood request.');
    } finally {
      if (mounted) {
        setState(() {
          _closing = false;
        });
      }
    }
  }

  Future<void> _deleteRequest(BloodRequest request) async {
    final confirmed = await _confirm(
      title: 'Delete request?',
      message:
          'This permanently deletes this blood request. '
          'This action cannot be undone.',
      confirmLabel: 'Delete',
      destructive: true,
    );

    if (confirmed != true || !mounted) {
      return;
    }

    setState(() {
      _deleting = true;
    });

    try {
      await ref.read(bloodRequestsRepositoryProvider).deleteRequest(request.id);

      ref.invalidate(bloodRequestProvider(widget.requestId));

      ref.invalidate(bloodRequestsProvider);

      if (!mounted) return;

      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Blood request deleted.')));

      context.pop(true);
    } on ApiException catch (error) {
      if (!mounted) return;

      _showError(error.message);
    } catch (_) {
      if (!mounted) return;

      _showError('Could not delete the blood request.');
    } finally {
      if (mounted) {
        setState(() {
          _deleting = false;
        });
      }
    }
  }

  Future<bool?> _confirm({
    required String title,
    required String message,
    required String confirmLabel,
    bool destructive = false,
  }) {
    return showDialog<bool>(
      context: context,
      builder: (context) {
        return AlertDialog(
          title: Text(title),
          content: Text(message),
          actions: [
            TextButton(
              onPressed: () {
                Navigator.of(context).pop(false);
              },
              child: const Text('Cancel'),
            ),
            FilledButton(
              style: destructive
                  ? FilledButton.styleFrom(backgroundColor: AppColors.critical)
                  : null,
              onPressed: () {
                Navigator.of(context).pop(true);
              },
              child: Text(confirmLabel),
            ),
          ],
        );
      },
    );
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final requestAsync = ref.watch(bloodRequestProvider(widget.requestId));

    final role = ref.watch(authControllerProvider).role;

    final canManage = role == 'staff' || role == 'admin';

    return Scaffold(
      appBar: AppBar(title: Text('Request details', style: AppText.title)),
      body: requestAsync.when(
        loading: () {
          return const Center(child: CircularProgressIndicator());
        },
        error: (error, _) {
          return ErrorState(
            message: error is ApiException
                ? error.message
                : 'We couldn’t load this request.',
            onRetry: _refresh,
          );
        },
        data: (request) {
          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: _refresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.gutter,
                AppSpacing.md,
                AppSpacing.gutter,
                AppSpacing.lg,
              ),
              children: [
                _RequestDetails(request: request),

                if (canManage) ...[
                  const SizedBox(height: AppSpacing.lg),

                  const SectionLabel('Manage request'),

                  AppCard(
                    padding: const EdgeInsets.all(AppSpacing.md),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          'Update the operational status, '
                          'monitor the coordinator workflow, '
                          'or close/delete the request.',
                          style: AppText.bodySmall,
                        ),

                        const SizedBox(height: AppSpacing.md),

                        DropdownButtonFormField<String>(
                          initialValue: _selectedStatus,
                          decoration: const InputDecoration(
                            labelText: 'Update status',
                            hintText: 'Select new status',
                          ),
                          items: _requestStatuses.map((status) {
                            return DropdownMenuItem<String>(
                              value: status,
                              child: Text(_titleCase(status)),
                            );
                          }).toList(),
                          onChanged: _updatingStatus || _closing || _deleting
                              ? null
                              : (value) {
                                  setState(() {
                                    _selectedStatus = value;
                                  });
                                },
                        ),

                        const SizedBox(height: AppSpacing.sm),

                        FilledButton(
                          onPressed: _updatingStatus || _closing || _deleting
                              ? null
                              : () {
                                  _updateStatus(request);
                                },
                          child: _updatingStatus
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: AppColors.onPrimary,
                                  ),
                                )
                              : const Text('Update Status'),
                        ),

                        const SizedBox(height: AppSpacing.sm),

                        OutlinedButton.icon(
                          onPressed: _updatingStatus || _closing || _deleting
                              ? null
                              : () {
                                  context.push(
                                    '/blood-requests/${request.id}/workflow',
                                  );
                                },
                          icon: const Icon(Icons.account_tree_outlined),
                          label: const Text('View Coordinator Workflow'),
                        ),

                        const SizedBox(height: AppSpacing.md),

                        const Divider(),

                        const SizedBox(height: AppSpacing.md),

                        OutlinedButton(
                          onPressed:
                              request.status.toLowerCase() == 'cancelled' ||
                                  _updatingStatus ||
                                  _closing ||
                                  _deleting
                              ? null
                              : () {
                                  _closeRequest(request);
                                },
                          child: _closing
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Text('Close Request'),
                        ),

                        const SizedBox(height: AppSpacing.sm),

                        OutlinedButton.icon(
                          style: OutlinedButton.styleFrom(
                            foregroundColor: AppColors.critical,
                            side: const BorderSide(color: AppColors.critical),
                          ),
                          onPressed: _updatingStatus || _closing || _deleting
                              ? null
                              : () {
                                  _deleteRequest(request);
                                },
                          icon: const Icon(Icons.delete_outline),
                          label: _deleting
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                  ),
                                )
                              : const Text('Delete Request'),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

class _RequestDetails extends StatelessWidget {
  const _RequestDetails({required this.request});

  final BloodRequest request;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        AppCard(
          accent: statusStyle(request.status).color,
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(request.hospitalName, style: AppText.title),

              const SizedBox(height: AppSpacing.sm),

              Wrap(
                spacing: AppSpacing.xs,
                runSpacing: AppSpacing.xs,
                children: [
                  BloodRequestStatusPill(request.status),
                  BloodRequestUrgencyPill(request.urgency),
                ],
              ),

              const SizedBox(height: AppSpacing.md),

              Row(
                children: [
                  Expanded(
                    child: _DetailValue(
                      label: 'Blood type',
                      value: request.bloodType,
                    ),
                  ),
                  Expanded(
                    child: _DetailValue(
                      label: 'Units requested',
                      value: '${request.unitsRequested}',
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),

        const SizedBox(height: AppSpacing.lg),

        const SectionLabel('Location'),

        AppCard(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Row(
            children: [
              const Icon(Icons.location_on_outlined, color: AppColors.agent),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  '${request.latitude.toStringAsFixed(4)}, ${request.longitude.toStringAsFixed(4)}',
                  style: AppText.bodyStrong,
                ),
              ),
            ],
          ),
        ),

        if (request.notes.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.lg),

          const SectionLabel('Notes'),

          AppCard(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Text(request.notes, style: AppText.body),
          ),
        ],

        const SizedBox(height: AppSpacing.lg),

        const SectionLabel('Timeline'),

        AppCard(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            children: [
              _TimelineValue(
                label: 'Created',
                value: isoDate(request.createdAt),
              ),

              if (request.fulfilledAt != null)
                _TimelineValue(
                  label: 'Fulfilled',
                  value: isoDate(request.fulfilledAt!),
                ),

              if (request.closedAt != null)
                _TimelineValue(
                  label: 'Closed',
                  value: isoDate(request.closedAt!),
                ),
            ],
          ),
        ),
      ],
    );
  }
}

class _DetailValue extends StatelessWidget {
  const _DetailValue({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: AppText.caption),
        const SizedBox(height: AppSpacing.xxs),
        Text(value, style: AppText.bodyStrong, overflow: TextOverflow.ellipsis),
      ],
    );
  }
}

class _TimelineValue extends StatelessWidget {
  const _TimelineValue({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Row(
        children: [
          Expanded(child: Text(label, style: AppText.bodySmall)),
          Text(value, style: AppText.bodyStrong),
        ],
      ),
    );
  }
}

String _titleCase(String value) {
  if (value.isEmpty) {
    return value;
  }

  return value
      .split('_')
      .map((part) {
        if (part.isEmpty) {
          return part;
        }

        return '${part[0].toUpperCase()}${part.substring(1)}';
      })
      .join(' ');
}
