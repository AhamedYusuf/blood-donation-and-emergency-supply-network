import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/date_format.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../../widgets/app_card.dart';
import '../../widgets/states.dart';
import '../auth/auth_controller.dart';
import 'blood_request_ui.dart';
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

class CoordinatorWorkflowScreen extends ConsumerStatefulWidget {
  const CoordinatorWorkflowScreen({super.key, required this.requestId});

  final String requestId;

  @override
  ConsumerState<CoordinatorWorkflowScreen> createState() =>
      _CoordinatorWorkflowScreenState();
}

class _CoordinatorWorkflowScreenState
    extends ConsumerState<CoordinatorWorkflowScreen> {
  bool _processingDecision = false;

  Future<void> _refresh() async {
    ref.invalidate(workflowMonitorProvider(widget.requestId));
    await ref.read(workflowMonitorProvider(widget.requestId).future);
  }

  Future<void> _handleDecision({
    required AgentWorkflow workflow,
    required String decision,
  }) async {
    final comments = await _showDecisionDialog(decision);
    if (comments == null || !mounted) return;

    setState(() => _processingDecision = true);
    try {
      final repository = ref.read(workflowRepositoryProvider);
      switch (decision) {
        case 'approve':
          await repository.approveWorkflow(
            workflowId: workflow.id,
            comments: comments,
          );
          break;
        case 'reject':
          await repository.rejectWorkflow(
            workflowId: workflow.id,
            comments: comments,
          );
          break;
        case 'revise':
          await repository.reviseWorkflow(
            workflowId: workflow.id,
            comments: comments,
          );
          break;
      }

      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(_successMessage(decision))));
      await _refresh();
    } on ApiException catch (error) {
      if (mounted) _showError(error.message);
    } catch (_) {
      if (mounted) _showError('Could not process the workflow decision.');
    } finally {
      if (mounted) setState(() => _processingDecision = false);
    }
  }

  Future<String?> _showDecisionDialog(String decision) async {
    final controller = TextEditingController();
    final result = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(_dialogTitle(decision)),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(_dialogMessage(decision)),
            const SizedBox(height: AppSpacing.md),
            TextField(
              controller: controller,
              maxLines: 3,
              decoration: const InputDecoration(
                labelText: 'Comments (optional)',
                hintText: 'Add a short comment',
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: decision == 'reject'
                ? FilledButton.styleFrom(backgroundColor: AppColors.critical)
                : null,
            onPressed: () =>
                Navigator.of(dialogContext).pop(controller.text.trim()),
            child: Text(_buttonLabel(decision)),
          ),
        ],
      ),
    );
    controller.dispose();
    return result;
  }

  void _showError(String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final role = ref.watch(authControllerProvider).role;
    final canManage = role == 'staff' || role == 'admin';
    final workflowAsync = canManage
        ? ref.watch(workflowMonitorProvider(widget.requestId))
        : null;

    return Scaffold(
      appBar: AppBar(title: Text('Coordinator Workflow', style: AppText.title)),
      body: !canManage
          ? Center(
              child: Padding(
                padding: const EdgeInsets.all(AppSpacing.gutter),
                child: Text(
                  'Coordinator workflow monitoring is available to staff and admins.',
                  style: AppText.body,
                  textAlign: TextAlign.center,
                ),
              ),
            )
          : workflowAsync!.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => ErrorState(
                message: error is ApiException
                    ? error.message
                    : 'We could not load this workflow.',
                onRetry: _refresh,
              ),
              data: (data) {
                if (data == null) {
                  return EmptyState(
                    icon: Icons.account_tree_outlined,
                    title: 'No workflow yet',
                    message: 'The coordinator has not started a workflow for this request.',
                    action: OutlinedButton.icon(
                      onPressed: _refresh,
                      icon: const Icon(Icons.refresh),
                      label: const Text('Refresh'),
                    ),
                  );
                }

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
                      _WorkflowContent(data: data),
                      if (_isAwaitingApproval(data.workflow.status)) ...[
                        const SizedBox(height: AppSpacing.lg),
                        _ApprovalActions(
                          processing: _processingDecision,
                          onApprove: () => _handleDecision(
                            workflow: data.workflow,
                            decision: 'approve',
                          ),
                          onReject: () => _handleDecision(
                            workflow: data.workflow,
                            decision: 'reject',
                          ),
                          onRevise: () => _handleDecision(
                            workflow: data.workflow,
                            decision: 'revise',
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

class _ApprovalActions extends StatelessWidget {
  const _ApprovalActions({
    required this.processing,
    required this.onApprove,
    required this.onReject,
    required this.onRevise,
  });

  final bool processing;
  final VoidCallback onApprove;
  final VoidCallback onReject;
  final VoidCallback onRevise;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SectionLabel('Human approval'),
        AppCard(
          accent: AppColors.agent,
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'The coordinator is waiting for a staff or admin decision.',
                style: AppText.bodySmall,
              ),
              const SizedBox(height: AppSpacing.md),
              FilledButton.icon(
                onPressed: processing ? null : onApprove,
                icon: processing
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: AppColors.onPrimary,
                        ),
                      )
                    : const Icon(Icons.check_circle_outline),
                label: const Text('Approve'),
              ),
              const SizedBox(height: AppSpacing.sm),
              OutlinedButton.icon(
                onPressed: processing ? null : onRevise,
                icon: const Icon(Icons.edit_note_outlined),
                label: const Text('Request Revision'),
              ),
              const SizedBox(height: AppSpacing.sm),
              OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  foregroundColor: AppColors.critical,
                  side: const BorderSide(color: AppColors.critical),
                ),
                onPressed: processing ? null : onReject,
                icon: const Icon(Icons.cancel_outlined),
                label: const Text('Reject'),
              ),
            ],
          ),
        ),
      ],
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
    final decision = summary.latestDecision;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        AppCard(
          accent: AppColors.agent,
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const Icon(
                    Icons.auto_awesome_outlined,
                    color: AppColors.agent,
                    size: 20,
                  ),
                  const SizedBox(width: AppSpacing.xs),
                  Text(
                    'AI-assisted workflow',
                    style: AppText.bodySmall.copyWith(color: AppColors.agent),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              Text('Coordinator Workflow', style: AppText.headline),
              const SizedBox(height: AppSpacing.sm),
              BloodRequestStatusPill(workflow.status),
              const SizedBox(height: AppSpacing.md),
              Text(workflow.objective, style: AppText.body),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.lg),
        const SectionLabel('Workflow progress'),
        AppCard(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: _WorkflowProgress(workflow: workflow, steps: data.steps),
        ),
        const SizedBox(height: AppSpacing.lg),
        const SectionLabel('Workflow summary'),
        AppCard(
          padding: const EdgeInsets.all(AppSpacing.md),
          child: Column(
            children: [
              _InfoRow(
                label: 'Current agent',
                value: workflow.currentAgent ?? 'Not active',
              ),
              _InfoRow(
                label: 'Revision count',
                value: '${summary.revisionCount}',
              ),
              _InfoRow(
                label: 'Started',
                value: _formatDate(workflow.startedAt),
              ),
              _InfoRow(
                label: 'Updated',
                value: _formatDate(workflow.updatedAt),
              ),
              _InfoRow(
                label: 'Latest decision',
                value: decision?.decision ?? 'No decision recorded',
              ),
              _InfoRow(label: 'Final outcome', value: summary.status),
              if (decision?.comments?.isNotEmpty == true)
                _InfoRow(
                  label: 'Decision comments',
                  value: decision!.comments!,
                ),
              if (workflow.failureReason?.isNotEmpty == true)
                _InfoRow(
                  label: 'Failure reason',
                  value: workflow.failureReason!,
                ),
            ],
          ),
        ),
        const SizedBox(height: AppSpacing.md),
        AppCard(
          padding: EdgeInsets.zero,
          child: Material(
            color: AppColors.surface,
            child: ExpansionTile(
              title: const Text('Technical Details', style: AppText.bodyStrong),
              childrenPadding: const EdgeInsets.fromLTRB(
                AppSpacing.md,
                0,
                AppSpacing.md,
                AppSpacing.md,
              ),
              children: [
                _InfoRow(label: 'Workflow ID', value: workflow.id),
                _InfoRow(label: 'Request ID', value: workflow.bloodRequestId),
              ],
            ),
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

class _WorkflowProgress extends StatelessWidget {
  const _WorkflowProgress({required this.workflow, required this.steps});

  final AgentWorkflow workflow;
  final List<WorkflowStep> steps;

  static const _stages = [
    'Stock Check',
    'Donor Search',
    'Eligibility Validation',
    'Human Approval',
    'Dispatch',
    'Complete',
  ];

  @override
  Widget build(BuildContext context) {
    final current = _currentStage();
    return Column(
      children: [
        for (var index = 0; index < _stages.length; index++)
          _ProgressStage(
            label: _stages[index],
            state: _stageState(index, current),
            last: index == _stages.length - 1,
          ),
      ],
    );
  }

  int _currentStage() {
    final status = normalizeBloodLabel(workflow.status);
    if (status == 'awaiting_approval') return 3;
    if (const {
      'completed',
      'complete',
      'fulfilled',
      'succeeded',
    }.contains(status)) {
      return 5;
    }

    final agent = normalizeBloodLabel(workflow.currentAgent ?? '');
    for (var index = 0; index < _stages.length; index++) {
      if (agent.isNotEmpty && _stageMatches(index, agent)) return index;
    }

    var completed = 0;
    for (var index = 0; index < _stages.length; index++) {
      final step = _stepFor(index);
      if (step != null && _isCompleted(step.status)) completed = index + 1;
    }
    return completed < _stages.length ? completed : _stages.length - 1;
  }

  _ProgressState _stageState(int index, int current) {
    final step = _stepFor(index);
    final status = normalizeBloodLabel(step?.status ?? '');
    if (const {'failed', 'rejected', 'error'}.contains(status)) {
      return _ProgressState.failed;
    }
    if (step != null && _isCompleted(step.status)) {
      return _ProgressState.complete;
    }
    if (index == current) return _ProgressState.current;
    return _ProgressState.upcoming;
  }

  WorkflowStep? _stepFor(int index) {
    for (final step in steps) {
      if (_stageMatches(index, normalizeBloodLabel(step.stepName))) return step;
    }
    return null;
  }

  bool _stageMatches(int index, String value) {
    final tokens = switch (index) {
      0 => const ['stock', 'inventory'],
      1 => const ['donor', 'search', 'matching'],
      2 => const ['eligibility', 'validation'],
      3 => const ['approval', 'review', 'human'],
      4 => const ['dispatch', 'notify'],
      _ => const ['complete', 'completion'],
    };
    return tokens.any(value.contains);
  }
}

enum _ProgressState { complete, current, upcoming, failed }

class _ProgressStage extends StatelessWidget {
  const _ProgressStage({
    required this.label,
    required this.state,
    required this.last,
  });

  final String label;
  final _ProgressState state;
  final bool last;

  @override
  Widget build(BuildContext context) {
    final complete = state == _ProgressState.complete;
    final failed = state == _ProgressState.failed;
    final current = state == _ProgressState.current;
    final color = failed
        ? AppColors.critical
        : complete
        ? AppColors.success
        : current
        ? AppColors.agent
        : AppColors.inkFaint;
    final icon = failed
        ? Icons.close
        : complete
        ? Icons.check
        : Icons.circle;

    return Column(
      children: [
        Row(
          children: [
            Container(
              width: 28,
              height: 28,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: color.withValues(alpha: current || failed ? 0.12 : 0.08),
                shape: BoxShape.circle,
                border: current ? Border.all(color: color) : null,
              ),
              child: Icon(
                icon,
                size: complete || failed ? 17 : 9,
                color: color,
              ),
            ),
            const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: Text(
                label,
                style: AppText.bodyStrong.copyWith(
                  color: state == _ProgressState.upcoming
                      ? AppColors.inkMuted
                      : AppColors.ink,
                ),
              ),
            ),
            if (current)
              Text(
                'Current',
                style: AppText.caption.copyWith(color: AppColors.agent),
              ),
            if (failed)
              Text(
                'Failed',
                style: AppText.caption.copyWith(color: AppColors.critical),
              ),
          ],
        ),
        if (!last)
          Container(
            width: 2,
            height: 20,
            margin: const EdgeInsets.only(left: 13, right: 13),
            color: complete
                ? AppColors.success.withValues(alpha: 0.55)
                : AppColors.hairline,
          ),
      ],
    );
  }
}

bool _isCompleted(String status) => const {
  'completed',
  'complete',
  'succeeded',
  'success',
}.contains(normalizeBloodLabel(status));

class _StepCard extends StatelessWidget {
  const _StepCard({required this.step});

  final WorkflowStep step;

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: EdgeInsets.zero,
      child: Material(
        color: AppColors.surface,
        child: ExpansionTile(
          title: Text(step.stepName, style: AppText.bodyStrong),
          subtitle: Text(
            '${step.agentName} · ${bloodLabel(step.status)}',
            style: AppText.caption,
          ),
          childrenPadding: const EdgeInsets.fromLTRB(
            AppSpacing.md,
            0,
            AppSpacing.md,
            AppSpacing.md,
          ),
          children: [
            if (step.narrative?.isNotEmpty == true)
              Padding(
                padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Text(step.narrative!, style: AppText.body),
                ),
              ),
            _InfoRow(label: 'Started', value: _formatDate(step.startedAt)),
            _InfoRow(label: 'Completed', value: _formatDate(step.completedAt)),
            _InfoRow(label: 'Retries', value: '${step.retryCount}'),
            if (step.errorMessage?.isNotEmpty == true)
              _InfoRow(label: 'Error', value: step.errorMessage!),
          ],
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.xs),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(width: 108, child: Text(label, style: AppText.bodySmall)),
          Expanded(
            child: Text(value, style: AppText.bodyStrong, softWrap: true),
          ),
        ],
      ),
    );
  }
}

bool _isAwaitingApproval(String status) =>
    normalizeBloodLabel(status) == 'awaiting_approval';

String _dialogTitle(String decision) => switch (decision) {
  'approve' => 'Approve workflow?',
  'reject' => 'Reject workflow?',
  'revise' => 'Request revision?',
  _ => 'Workflow decision',
};

String _dialogMessage(String decision) => switch (decision) {
  'approve' =>
    'Approving will allow the coordinator workflow to continue to dispatch.',
  'reject' => 'Rejecting will stop this workflow.',
  'revise' => 'The coordinator will revise and run the workflow again.',
  _ => '',
};

String _buttonLabel(String decision) => switch (decision) {
  'approve' => 'Approve',
  'reject' => 'Reject',
  'revise' => 'Revise',
  _ => 'Continue',
};

String _successMessage(String decision) => switch (decision) {
  'approve' => 'Workflow approved.',
  'reject' => 'Workflow rejected.',
  'revise' => 'Workflow revision requested.',
  _ => 'Workflow updated.',
};

String _formatDate(DateTime? value) =>
    value == null ? 'Not recorded' : isoDate(value.toLocal());
