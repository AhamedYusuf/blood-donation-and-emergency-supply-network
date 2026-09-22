export interface AgentWorkflow {
  id: string;
  bloodRequestId: string;
  objective: string;
  currentAgent?: string | null;
  status: string;
  revisionCount: number;
  startedAt: string;
  updatedAt: string;
  completedAt?: string | null;
  failureReason?: string | null;
}

export interface AgentStep {
  id: string;
  workflowId: string;
  agentName: string;
  stepName: string;
  status: string;
  inputJson?: string | null;
  outputJson?: string | null;
  narrative?: string | null;
  startedAt: string;
  completedAt?: string | null;
  errorMessage?: string | null;
  retryCount: number;
}

export interface WorkflowSummary {
  id: string;
  bloodRequestId: string;
  objective: string;
  currentAgent?: string | null;
  status: string;
  revisionCount: number;
  startedAt: string;
  updatedAt: string;
  completedAt?: string | null;
  failureReason?: string | null;

  totalSteps: number;
  completedSteps: number;
  failedSteps: number;

  latestDecision?: {
    decision: string;
    comments?: string | null;
    decidedByUserId: string;
    createdAt: string;
  } | null;
}

export interface WorkflowDecisionRequest {
  comments?: string | null;
}