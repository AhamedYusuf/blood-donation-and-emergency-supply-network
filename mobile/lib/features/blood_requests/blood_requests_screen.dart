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
import '../auth/auth_controller.dart';

final bloodRequestsProvider = FutureProvider.autoDispose<List<BloodRequest>>((ref) {
  return ref.watch(bloodRequestsRepositoryProvider).getRequests();
});

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
      appBar: AppBar(
        title: Text('Blood requests', style: AppText.title),
      ),
      floatingActionButton: canCreate
          ? FloatingActionButton(
              onPressed: () async {
                final created = await context.push<bool>('/blood-requests/new');
                if (created == true) ref.invalidate(bloodRequestsProvider);
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
                    constraints: BoxConstraints(minHeight: constraints.maxHeight),
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
              itemCount: requests.length,
              separatorBuilder: (_, _) => const SizedBox(height: AppSpacing.xs),
              itemBuilder: (context, index) => _RequestCard(
                request: requests[index],
                onTap: () => context.push('/blood-requests/${requests[index].id}'),
              ),
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
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  request.hospitalName,
                  style: AppText.bodyStrong,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              StatusPill(request.status),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Row(
            children: [
              _RequestValue(label: 'Blood type', value: request.bloodType),
              const SizedBox(width: AppSpacing.lg),
              _RequestValue(label: 'Units', value: '${request.unitsRequested}'),
              const SizedBox(width: AppSpacing.lg),
              _RequestValue(label: 'Urgency', value: _titleCase(request.urgency)),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          Row(
            children: [
              const Icon(Icons.schedule_outlined, size: 16, color: AppColors.inkFaint),
              const SizedBox(width: AppSpacing.xs),
              Text('Created ${isoDate(request.createdAt)}', style: AppText.caption),
            ],
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
          Text(value, style: AppText.bodyStrong, overflow: TextOverflow.ellipsis),
        ],
      ),
    );
  }
}

String _titleCase(String value) {
  if (value.isEmpty) return value;
  return value
      .split('_')
      .map((part) => part.isEmpty ? part : '${part[0].toUpperCase()}${part.substring(1)}')
      .join(' ');
}