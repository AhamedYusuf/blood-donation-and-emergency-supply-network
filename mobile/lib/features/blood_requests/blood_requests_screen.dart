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

class BloodRequestsScreen extends ConsumerWidget {
  const BloodRequestsScreen({super.key});

  Future<void> _refresh(WidgetRef ref) async {
    ref.invalidate(bloodRequestsProvider);
    await ref.read(bloodRequestsProvider.future);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final requestsAsync = ref.watch(bloodRequestsProvider);
    final role = ref.watch(authControllerProvider).role;
    final canCreate = role == 'staff' || role == 'admin';

    return Scaffold(
      appBar: AppBar(title: Text('Blood Requests', style: AppText.title)),
      floatingActionButton: canCreate
          ? FloatingActionButton(
              onPressed: () async {
                final created = await context.push<bool>('/blood-requests/new');
                if (created == true) {
                  ref.invalidate(bloodRequestsProvider);
                }
              },
              backgroundColor: AppColors.primary,
              foregroundColor: AppColors.onPrimary,
              child: const Icon(Icons.add),
            )
          : null,
      body: requestsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          message: error is ApiException
              ? error.message
              : 'We couldn’t load the blood requests.',
          onRetry: () => _refresh(ref),
        ),
        data: (requests) {
          if (requests.isEmpty) {
            return RefreshIndicator(
              color: AppColors.primary,
              onRefresh: () => _refresh(ref),
              child: LayoutBuilder(
                builder: (context, constraints) => SingleChildScrollView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  child: ConstrainedBox(
                    constraints: BoxConstraints(
                      minHeight: constraints.maxHeight,
                    ),
                    child: const EmptyState(
                      icon: Icons.bloodtype_outlined,
                      title: 'No blood requests',
                      message: 'There are no blood requests to show right now.',
                    ),
                  ),
                ),
              ),
            );
          }

          return RefreshIndicator(
            color: AppColors.primary,
            onRefresh: () => _refresh(ref),
            child: ListView.separated(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(
                AppSpacing.gutter,
                AppSpacing.md,
                AppSpacing.gutter,
                AppSpacing.gutter,
              ),
              itemCount: requests.length + 1,
              separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.xs),
              itemBuilder: (context, index) {
                if (index == 0) {
                  return _RequestOverview(requests: requests);
                }
                final request = requests[index - 1];
                return _RequestCard(
                  request: request,
                  onTap: () async {
                    await context.push<bool>('/blood-requests/${request.id}');
                    ref.invalidate(bloodRequestsProvider);
                  },
                );
              },
            ),
          );
        },
      ),
    );
  }
}

class _RequestCard extends StatelessWidget {
  const _RequestCard({required this.request, required this.onTap});

  final BloodRequest request;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      onTap: onTap,
      accent: statusStyle(request.status).color,
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  request.hospitalName,
                  style: AppText.headline,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const SizedBox(width: AppSpacing.xs),
              const Icon(Icons.chevron_right, color: AppColors.inkFaint),
            ],
          ),
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
              _RequestValue(label: 'Blood type', value: request.bloodType),
              _RequestValue(
                label: 'Units requested',
                value: '${request.unitsRequested}',
              ),
              const Spacer(),
              Text(isoDate(request.createdAt), style: AppText.caption),
            ],
          ),
        ],
      ),
    );
  }
}

class _RequestOverview extends StatelessWidget {
  const _RequestOverview({required this.requests});

  final List<BloodRequest> requests;

  @override
  Widget build(BuildContext context) {
    final open = requests
        .where((r) => normalizeBloodLabel(r.status) == 'open')
        .length;
    final awaiting = requests
        .where((r) => normalizeBloodLabel(r.status) == 'awaiting_approval')
        .length;
    final fulfilled = requests
        .where((r) => normalizeBloodLabel(r.status) == 'fulfilled')
        .length;
    final active = requests
        .where(
          (r) => const {
            'matching',
            'donors_notified',
            'partially_fulfilled',
          }.contains(normalizeBloodLabel(r.status)),
        )
        .length;

    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Hospital demand', style: AppText.headline),
          const SizedBox(height: AppSpacing.xxs),
          Text('Requests across your network', style: AppText.bodySmall),
          const SizedBox(height: AppSpacing.md),
          LayoutBuilder(
            builder: (context, constraints) {
              final width = (constraints.maxWidth - AppSpacing.xs) / 2;
              return Wrap(
                spacing: AppSpacing.xs,
                runSpacing: AppSpacing.xs,
                children: [
                  _SummaryValue(label: 'Open', value: open, width: width),
                  _SummaryValue(
                    label: 'Awaiting approval',
                    value: awaiting,
                    width: width,
                  ),
                  _SummaryValue(label: 'Active', value: active, width: width),
                  _SummaryValue(
                    label: 'Fulfilled',
                    value: fulfilled,
                    width: width,
                  ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

class _SummaryValue extends StatelessWidget {
  const _SummaryValue({
    required this.label,
    required this.value,
    required this.width,
  });

  final String label;
  final int value;
  final double width;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: width,
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: AppSpacing.xs,
      ),
      decoration: BoxDecoration(
        color: AppColors.surface,
        border: Border.all(color: AppColors.hairline),
        borderRadius: BorderRadius.circular(AppRadii.sm),
      ),
      child: Row(
        children: [
          Text('$value', style: AppText.bodyStrong),
          const SizedBox(width: AppSpacing.xs),
          Expanded(
            child: Text(
              label,
              style: AppText.caption,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ),
    );
  }
}

class _RequestValue extends StatelessWidget {
  const _RequestValue({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: AppText.caption),
          const SizedBox(height: AppSpacing.xxs),
          Text(
            value,
            style: AppText.bodyStrong,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ),
    );
  }
}
