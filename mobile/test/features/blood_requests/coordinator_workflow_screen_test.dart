import 'dart:async';

import 'package:blood_donation_network/features/blood_requests/coordinator_workflow_screen.dart';
import 'package:blood_donation_network/features/blood_requests/workflow_models.dart';
import 'package:blood_donation_network/theme/app_theme.dart';
import 'package:blood_donation_network/widgets/status_pill.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

WorkflowMonitorData _completedWorkflow() => WorkflowMonitorData(
  workflow: AgentWorkflow(
    id: 'workflow-1',
    bloodRequestId: 'request-1',
    objective: 'Coordinate emergency blood supply',
    status: 'completed',
    revisionCount: 2,
    startedAt: DateTime(2026, 9, 25, 10),
    updatedAt: DateTime(2026, 9, 25, 11),
    completedAt: DateTime(2026, 9, 25, 11),
  ),
  steps: [
    WorkflowStep(
      id: 'step-1',
      workflowId: 'workflow-1',
      agentName: 'StockCheckAgent',
      stepName: 'Stock check',
      status: 'completed',
      retryCount: 0,
      startedAt: DateTime(2026, 9, 25, 10),
      completedAt: DateTime(2026, 9, 25, 10, 5),
    ),
  ],
  summary: WorkflowSummary(
    id: 'workflow-1',
    bloodRequestId: 'request-1',
    objective: 'Coordinate emergency blood supply',
    status: 'completed',
    revisionCount: 2,
    totalSteps: 1,
    completedSteps: 1,
    failedSteps: 0,
    latestDecision: WorkflowDecision(
      decision: 'approved',
      decidedByUserId: 'staff-1',
      createdAt: DateTime(2026, 9, 25, 10, 30),
    ),
  ),
);

Widget _host(Override workflowOverride) => ProviderScope(
  overrides: [workflowOverride],
  child: MaterialApp(
    theme: buildAppTheme(),
    home: const CoordinatorWorkflowScreen(requestId: 'request-1'),
  ),
);

void main() {
  testWidgets('shows loading state while workflow is pending', (tester) async {
    final pending = Completer<WorkflowMonitorData?>();
    await tester.pumpWidget(
      _host(
        workflowMonitorProvider('request-1')
            .overrideWith((ref) => pending.future),
      ),
    );
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    pending.complete(null);
  });

  testWidgets('renders completed status, approval, revision, and history', (
    tester,
  ) async {
    await tester.pumpWidget(
      _host(
        workflowMonitorProvider('request-1')
            .overrideWith((ref) async => _completedWorkflow()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byType(StatusPill), findsNWidgets(2));
    expect(find.text('completed'), findsOneWidget);
    expect(find.text('approved'), findsOneWidget);
    expect(find.text('2'), findsOneWidget);
    expect(find.text('Stock check'), findsOneWidget);
    expect(find.text('StockCheckAgent'), findsOneWidget);
  });
}
