import 'package:blood_donation_network/features/blood_requests/workflow_models.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('parses an agent workflow with nullable terminal fields', () {
    final workflow = AgentWorkflow.fromJson({
      'id': 'workflow-1',
      'bloodRequestId': 'request-1',
      'objective': 'Coordinate emergency blood supply',
      'currentAgent': null,
      'status': 'completed',
      'revisionCount': 2,
      'startedAt': '2026-09-25T10:00:00Z',
      'updatedAt': '2026-09-25T11:00:00Z',
      'completedAt': '2026-09-25T11:00:00Z',
      'failureReason': null,
    });

    expect(workflow.id, 'workflow-1');
    expect(workflow.bloodRequestId, 'request-1');
    expect(workflow.objective, 'Coordinate emergency blood supply');
    expect(workflow.status, 'completed');
    expect(workflow.revisionCount, 2);
    expect(workflow.currentAgent, isNull);
    expect(workflow.completedAt, isNotNull);
    expect(workflow.failureReason, isNull);
  });

  test('parses a workflow step and nullable history fields', () {
    final step = WorkflowStep.fromJson({
      'id': 'step-1',
      'workflowId': 'workflow-1',
      'agentName': 'StockCheckAgent',
      'stepName': 'stock_check',
      'status': 'completed',
      'inputJson': '{"bloodType":"O+"}',
      'outputJson': '{"available":true}',
      'narrative': 'Inventory was checked.',
      'retryCount': 1,
      'startedAt': '2026-09-25T10:01:00Z',
      'completedAt': null,
      'errorMessage': null,
    });

    expect(step.agentName, 'StockCheckAgent');
    expect(step.stepName, 'stock_check');
    expect(step.narrative, 'Inventory was checked.');
    expect(step.retryCount, 1);
    expect(step.completedAt, isNull);
    expect(step.errorMessage, isNull);
  });

  test('parses summary counts and latest approval decision', () {
    final summary = WorkflowSummary.fromJson({
      'id': 'workflow-1',
      'bloodRequestId': 'request-1',
      'objective': 'Coordinate emergency blood supply',
      'currentAgent': null,
      'status': 'completed',
      'revisionCount': 2,
      'startedAt': '2026-09-25T10:00:00Z',
      'updatedAt': '2026-09-25T11:00:00Z',
      'completedAt': '2026-09-25T11:00:00Z',
      'failureReason': null,
      'totalSteps': 4,
      'completedSteps': 4,
      'failedSteps': 0,
      'latestDecision': {
        'decision': 'approved',
        'comments': 'Proceed with dispatch.',
        'decidedByUserId': 'staff-1',
        'createdAt': '2026-09-25T10:30:00Z',
      },
    });

    expect(summary.totalSteps, 4);
    expect(summary.completedSteps, 4);
    expect(summary.failedSteps, 0);
    expect(summary.latestDecision?.decision, 'approved');
    expect(summary.latestDecision?.comments, 'Proceed with dispatch.');
    expect(summary.completedAt, isNotNull);
    expect(summary.failureReason, isNull);
  });
}
