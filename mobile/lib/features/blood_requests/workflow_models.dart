class AgentWorkflow {
  const AgentWorkflow({
    required this.id,
    required this.bloodRequestId,
    required this.objective,
    required this.status,
    required this.revisionCount,
    required this.startedAt,
    required this.updatedAt,
    this.currentAgent,
    this.completedAt,
    this.failureReason,
  });

  final String id;
  final String bloodRequestId;
  final String objective;
  final String? currentAgent;
  final String status;
  final int revisionCount;
  final DateTime? startedAt;
  final DateTime? updatedAt;
  final DateTime? completedAt;
  final String? failureReason;

  factory AgentWorkflow.fromJson(Map<String, dynamic> json) => AgentWorkflow(
    id: _string(json['id']),
    bloodRequestId: _string(json['bloodRequestId']),
    objective: _string(json['objective']),
    currentAgent: _nullableString(json['currentAgent']),
    status: _string(json['status']),
    revisionCount: _int(json['revisionCount']),
    startedAt: _dateTime(json['startedAt']),
    updatedAt: _dateTime(json['updatedAt']),
    completedAt: _dateTime(json['completedAt']),
    failureReason: _nullableString(json['failureReason']),
  );
}

class WorkflowStep {
  const WorkflowStep({
    required this.id,
    required this.workflowId,
    required this.agentName,
    required this.stepName,
    required this.status,
    required this.retryCount,
    required this.startedAt,
    this.narrative,
    this.completedAt,
    this.errorMessage,
  });

  final String id;
  final String workflowId;
  final String agentName;
  final String stepName;
  final String status;
  final String? narrative;
  final int retryCount;
  final DateTime? startedAt;
  final DateTime? completedAt;
  final String? errorMessage;

  factory WorkflowStep.fromJson(Map<String, dynamic> json) => WorkflowStep(
    id: _string(json['id']),
    workflowId: _string(json['workflowId']),
    agentName: _string(json['agentName']),
    stepName: _string(json['stepName']),
    status: _string(json['status']),
    narrative: _nullableString(json['narrative']),
    retryCount: _int(json['retryCount']),
    startedAt: _dateTime(json['startedAt']),
    completedAt: _dateTime(json['completedAt']),
    errorMessage: _nullableString(json['errorMessage']),
  );
}

class WorkflowDecision {
  const WorkflowDecision({
    required this.decision,
    required this.decidedByUserId,
    required this.createdAt,
    this.comments,
  });

  final String decision;
  final String? comments;
  final String decidedByUserId;
  final DateTime? createdAt;

  factory WorkflowDecision.fromJson(Map<String, dynamic> json) =>
      WorkflowDecision(
        decision: _string(json['decision']),
        comments: _nullableString(json['comments']),
        decidedByUserId: _string(json['decidedByUserId']),
        createdAt: _dateTime(json['createdAt']),
      );
}

class WorkflowSummary {
  const WorkflowSummary({
    required this.id,
    required this.bloodRequestId,
    required this.objective,
    required this.status,
    required this.revisionCount,
    required this.totalSteps,
    required this.completedSteps,
    required this.failedSteps,
    this.currentAgent,
    this.startedAt,
    this.updatedAt,
    this.completedAt,
    this.failureReason,
    this.latestDecision,
  });

  final String id;
  final String bloodRequestId;
  final String objective;
  final String? currentAgent;
  final String status;
  final int revisionCount;
  final DateTime? startedAt;
  final DateTime? updatedAt;
  final DateTime? completedAt;
  final String? failureReason;
  final int totalSteps;
  final int completedSteps;
  final int failedSteps;
  final WorkflowDecision? latestDecision;

  factory WorkflowSummary.fromJson(Map<String, dynamic> json) =>
      WorkflowSummary(
        id: _string(json['id']),
        bloodRequestId: _string(json['bloodRequestId']),
        objective: _string(json['objective']),
        currentAgent: _nullableString(json['currentAgent']),
        status: _string(json['status']),
        revisionCount: _int(json['revisionCount']),
        startedAt: _dateTime(json['startedAt']),
        updatedAt: _dateTime(json['updatedAt']),
        completedAt: _dateTime(json['completedAt']),
        failureReason: _nullableString(json['failureReason']),
        totalSteps: _int(json['totalSteps']),
        completedSteps: _int(json['completedSteps']),
        failedSteps: _int(json['failedSteps']),
        latestDecision: _decision(json['latestDecision']),
      );
}

String _string(Object? value) => value?.toString() ?? '';

String? _nullableString(Object? value) => value?.toString();

int _int(Object? value) =>
    value is num ? value.toInt() : int.tryParse('$value') ?? 0;

DateTime? _dateTime(Object? value) =>
    value is String ? DateTime.tryParse(value) : null;

WorkflowDecision? _decision(Object? value) =>
    value is Map<String, dynamic> ? WorkflowDecision.fromJson(value) : null;
