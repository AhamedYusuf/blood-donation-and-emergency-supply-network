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
import 'blood_request.dart';
import 'blood_requests_repository.dart';

final bloodRequestProvider = FutureProvider.autoDispose
    .family<BloodRequest, String>((ref, id) {
      return ref.watch(bloodRequestsRepositoryProvider).getRequestById(id);
    });

class BloodRequestDetailsScreen extends ConsumerWidget {
  const BloodRequestDetailsScreen({super.key, required this.requestId});

  final String requestId;

  Future<void> _refresh(WidgetRef ref) async {
    ref.invalidate(bloodRequestProvider(requestId));
    await ref.read(bloodRequestProvider(requestId).future);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final requestAsync = ref.watch(bloodRequestProvider(requestId));

    return Scaffold(
      appBar: AppBar(title: Text('Request details', style: AppText.title)),
      body: requestAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          message: error is ApiException
              ? error.message
              : 'We couldn’t load this request.',
          onRetry: () => _refresh(ref),
        ),
        data: (request) => RefreshIndicator(
          color: AppColors.primary,
          onRefresh: () => _refresh(ref),
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.gutter,
              AppSpacing.md,
              AppSpacing.gutter,
              AppSpacing.lg,
            ),
            children: [_RequestDetails(request: request)],
          ),
        ),
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
              StatusPill(request.status),
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
                  Expanded(
                    child: _DetailValue(
                      label: 'Urgency',
                      value: _titleCase(request.urgency),
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
              Expanded(
                child: _DetailValue(
                  label: 'Latitude',
                  value: '${request.latitude}',
                ),
              ),
              Expanded(
                child: _DetailValue(
                  label: 'Longitude',
                  value: '${request.longitude}',
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
        const SizedBox(height: AppSpacing.lg),
        OutlinedButton.icon(
          onPressed: () =>
              context.push('/blood-requests/${request.id}/workflow'),
          icon: const Icon(Icons.account_tree_outlined),
          label: const Text('View Coordinator Workflow'),
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
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(label, style: AppText.caption),
      const SizedBox(height: AppSpacing.xxs),
      Text(value, style: AppText.bodyStrong, overflow: TextOverflow.ellipsis),
    ],
  );
}

class _TimelineValue extends StatelessWidget {
  const _TimelineValue({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: AppSpacing.sm),
    child: Row(
      children: [
        Expanded(child: Text(label, style: AppText.bodySmall)),
        Text(value, style: AppText.bodyStrong),
      ],
    ),
  );
}

String _titleCase(String value) =>
    value.isEmpty ? value : '${value[0].toUpperCase()}${value.substring(1)}';
