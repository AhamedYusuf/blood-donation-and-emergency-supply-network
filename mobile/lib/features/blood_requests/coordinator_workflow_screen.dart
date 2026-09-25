import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/date_format.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/states.dart';
import '../../widgets/status_pill.dart';
import 'workflow_models.dart';
import 'workflow_repository.dart';

class WorkflowMonitorData {
  const WorkflowMonitorData({
    required this.workflow,
    required this.steps,
    required this.summary,
  });

  final AgentWorkflow workflow;
  final List<WorkflowStep> steps;
  final WorkflowSummary summary;
}

final workflowMonitorProvider = FutureProvider.autoDispose
    .family<WorkflowMonitorData?, String>((ref, requestId) async {
      final repository = ref.watch(workflowRepositoryProvider);
      final workflow = await repository.getWorkflowByRequestId(requestId);
      if (workflow == null) return null;

      final steps = await repository.getWorkflowSteps(workflow.id);
      final summary = await repository.getWorkflowSummary(workflow.id);
      return WorkflowMonitorData(
        workflow: workflow,
        steps: steps,
        summary: summary,
      );
    });

class CoordinatorWorkflowScreen extends ConsumerWidget {
  const CoordinatorWorkflowScreen({super.key, required this.requestId});

  final String requestId;

  Future<void> _refresh(WidgetRef ref) async {
    ref.invalidate(workflowMonitorProvider(requestId));
    await ref.read(workflowMonitorProvider(requestId).future);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final workflowAsync = ref.watch(workflowMonitorProvider(requestId));

    return Scaffold(
      appBar: AppBar(title: Text('Coordinator workflow', style: AppText.title)),
      body: workflowAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorState(
          message: error is ApiException
              ? error.message
              : 'We could not load this workflow.',
          onRetry: () => _refresh(ref),
        ),
        data: (data) {
          if (data == null) {
            return EmptyState(
              icon: Icons.account_tree_outlined,
              title: 'No workflow yet',
              message: 'The coordinator has not started a workflow for this request.',
              action: OutlinedButton.icon(
                onPressed: () => _refresh(ref),
                icon: const Icon(Icons.refresh),
                label: const Text('Refresh'),
              ),
            );
          }

          return RefreshIndicator(
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
              children: [_WorkflowContent(data: data)],
            ),
          );
        },
      ),
    );
  }
}

class _WorkflowContent extends StatelessWidget {
  const _WorkflowContent({required this.data});

  final WorkflowMonitorData data;

  @override
  Widget build(BuildContext context) {
    final workflow = data.workflow;
    final summary = data.summary;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        AppCard(
          accent: AppColors.agent,
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Workflow status', style: AppText.headline),
              const SizedBox(height: AppSpacing.sm),
              StatusPill(workflow.status),
              const SizedBox(height: AppSpacing.md),
              _InfoRow(label: 'Workflow ID', value: workflow.id),
              _InfoRow(label: 'Request ID', value: workflow.bloodRequestId),
              _InfoRow(label: 'Objective', value: workflow.objective),
              _InfoRow(
                label: 'Current agent',
                value: workflow.currentAgent ?? 'Not active',
              ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        const SectionLabel('Execution summary'),
        AppCard(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            children: [
              _InfoRow(
                label: 'Approval status',
                value:
                    summary.latestDecision?.decision ?? 'No decision recorded',
              ),
              _InfoRow(
                label: 'Revision count',
                value: '${summary.revisionCount}',
              ),
              _InfoRow(label: 'Final outcome', value: summary.status),
              _InfoRow(
                label: 'Steps',
                value:
                    '${summary.completedSteps}/${summary.totalSteps} completed',
              ),
              _InfoRow(label: 'Failed steps', value: '${summary.failedSteps}'),
              if (summary.latestDecision?.comments?.isNotEmpty == true)
                _InfoRow(
                  label: 'Decision comments',
                  value: summary.latestDecision!.comments!,
                ),
              if (workflow.failureReason?.isNotEmpty == true)
                _InfoRow(
                  label: 'Failure reason',
                  value: workflow.failureReason!,
                ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        const SectionLabel('Workflow history'),
        if (data.steps.isEmpty)
          AppCard(
            padding: const EdgeInsets.all(AppSpacing.md),
            child: Text(
              'No workflow steps have been recorded.',
              style: AppText.body,
            ),
          )
        else
          ...data.steps.map(
            (step) => Padding(
              padding: const EdgeInsets.only(bottom: AppSpacing.xs),
              child: _StepCard(step: step),
            ),
          ),
      ],
    );
  }
}

class _StepCard extends StatelessWidget {
  const _StepCard({required this.step});

  final WorkflowStep step;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: const EdgeInsets.all(AppSpacing.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(child: Text(step.stepName, style: AppText.headline)),
              const SizedBox(width: AppSpacing.sm),
              StatusPill(step.status),
            ],
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(step.agentName, style: AppText.caption),
          if (step.narrative?.isNotEmpty == true) ...[
            const SizedBox(height: AppSpacing.sm),
            Text(step.narrative!, style: AppText.body),
          ],
          if (step.errorMessage?.isNotEmpty == true) ...[
            const SizedBox(height: AppSpacing.sm),
            Text(
              step.errorMessage!,
              style: AppText.body.copyWith(color: AppColors.critical),
            ),
          ],
          const SizedBox(height: AppSpacing.sm),
          _InfoRow(label: 'Started', value: _formatDate(step.startedAt)),
          _InfoRow(label: 'Completed', value: _formatDate(step.completedAt)),
          _InfoRow(label: 'Retries', value: '${step.retryCount}'),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: AppSpacing.xs),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(width: 125, child: Text(label, style: AppText.bodySmall)),
        Expanded(child: Text(value, style: AppText.bodyStrong)),
      ],
    ),
  );
}

String _formatDate(DateTime? value) =>
    value == null ? 'Not recorded' : isoDate(value.toLocal());
