import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../auth/auth_controller.dart';
import 'workflow_models.dart';

class WorkflowRepository {
  WorkflowRepository(this._ref);

  final Ref _ref;

  ApiClient get _api => _ref.read(apiClientProvider);
  String? get _token => _ref.read(authTokenProvider);

  /// GET /api/agent/workflows/request/{bloodRequestId}
  Future<AgentWorkflow?> getWorkflowByRequestId(
    String bloodRequestId,
  ) async {
    try {
      final json = await _api.get(
        '/api/agent/workflows/request/$bloodRequestId',
        token: _token,
      );

      return AgentWorkflow.fromJson(
        json as Map<String, dynamic>,
      );
    } on ApiException catch (error) {
      if (error.statusCode == 404) {
        return null;
      }

      rethrow;
    }
  }

  /// GET /api/agent/workflows/{id}
  Future<AgentWorkflow> getWorkflow(
    String workflowId,
  ) async {
    final json = await _api.get(
      '/api/agent/workflows/$workflowId',
      token: _token,
    );

    return AgentWorkflow.fromJson(
      json as Map<String, dynamic>,
    );
  }

  /// GET /api/agent/workflows/{id}/steps
  Future<List<WorkflowStep>> getWorkflowSteps(
    String workflowId,
  ) async {
    final json = await _api.get(
      '/api/agent/workflows/$workflowId/steps',
      token: _token,
    );

    final list = (json as List<dynamic>)
        .whereType<Map<String, dynamic>>();

    return list
        .map(WorkflowStep.fromJson)
        .toList();
  }

  /// GET /api/agent/workflows/{id}/summary
  Future<WorkflowSummary> getWorkflowSummary(
    String workflowId,
  ) async {
    final json = await _api.get(
      '/api/agent/workflows/$workflowId/summary',
      token: _token,
    );

    return WorkflowSummary.fromJson(
      json as Map<String, dynamic>,
    );
  }

  /// POST /api/agent/workflows/{id}/approve
  ///
  /// Staff/Admin approves the paused coordinator workflow.
  Future<void> approveWorkflow({
    required String workflowId,
    String? comments,
  }) async {
    await _api.post(
      '/api/agent/workflows/$workflowId/approve',
      token: _token,
      body: {
        if (comments != null &&
            comments.trim().isNotEmpty)
          'comments': comments.trim(),
      },
    );
  }

  /// POST /api/agent/workflows/{id}/reject
  ///
  /// Staff/Admin rejects the workflow.
  Future<void> rejectWorkflow({
    required String workflowId,
    String? comments,
  }) async {
    await _api.post(
      '/api/agent/workflows/$workflowId/reject',
      token: _token,
      body: {
        if (comments != null &&
            comments.trim().isNotEmpty)
          'comments': comments.trim(),
      },
    );
  }

  /// POST /api/agent/workflows/{id}/revise
  ///
  /// Staff/Admin asks the coordinator to revise the workflow.
  Future<void> reviseWorkflow({
    required String workflowId,
    String? comments,
  }) async {
    await _api.post(
      '/api/agent/workflows/$workflowId/revise',
      token: _token,
      body: {
        if (comments != null &&
            comments.trim().isNotEmpty)
          'comments': comments.trim(),
      },
    );
  }
}

final workflowRepositoryProvider =
    Provider<WorkflowRepository>(
  (ref) => WorkflowRepository(ref),
);