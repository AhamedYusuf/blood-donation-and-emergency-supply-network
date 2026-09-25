import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../auth/auth_controller.dart';
import 'workflow_models.dart';

class WorkflowRepository {
  WorkflowRepository(this._ref);

  final Ref _ref;

  ApiClient get _api => _ref.read(apiClientProvider);
  String? get _token => _ref.read(authTokenProvider);

  /// GET /api/agent/workflows/request/{bloodRequestId}.
  Future<AgentWorkflow?> getWorkflowByRequestId(String bloodRequestId) async {
    try {
      final json = await _api.get(
        '/api/agent/workflows/request/$bloodRequestId',
        token: _token,
      );
      return AgentWorkflow.fromJson(json as Map<String, dynamic>);
    } on ApiException catch (error) {
      if (error.statusCode == 404) return null;
      rethrow;
    }
  }

  /// GET /api/agent/workflows/{id}.
  Future<AgentWorkflow> getWorkflow(String workflowId) async {
    final json = await _api.get(
      '/api/agent/workflows/$workflowId',
      token: _token,
    );
    return AgentWorkflow.fromJson(json as Map<String, dynamic>);
  }

  /// GET /api/agent/workflows/{id}/steps.
  Future<List<WorkflowStep>> getWorkflowSteps(String workflowId) async {
    final json = await _api.get(
      '/api/agent/workflows/$workflowId/steps',
      token: _token,
    );
    final list = (json as List<dynamic>).whereType<Map<String, dynamic>>();
    return list.map(WorkflowStep.fromJson).toList();
  }

  /// GET /api/agent/workflows/{id}/summary.
  Future<WorkflowSummary> getWorkflowSummary(String workflowId) async {
    final json = await _api.get(
      '/api/agent/workflows/$workflowId/summary',
      token: _token,
    );
    return WorkflowSummary.fromJson(json as Map<String, dynamic>);
  }
}

final workflowRepositoryProvider = Provider<WorkflowRepository>(
  (ref) => WorkflowRepository(ref),
);
