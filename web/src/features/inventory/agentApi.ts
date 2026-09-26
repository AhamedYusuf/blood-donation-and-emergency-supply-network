import type {
  EmergencyInventoryRecommendation,
  EmergencyInventoryRequest,
  StockRiskResponse,
  BloodType,
} from "./inventoryTypes";

const AGENT_SERVICE_URL =
  import.meta.env.VITE_AGENT_SERVICE_URL ||
  "http://localhost:8001";

const BLOOD_TYPE_TO_AGENT_VALUE: Record<BloodType, string> = {
  APositive: "A+",
  ANegative: "A-",
  BPositive: "B+",
  BNegative: "B-",
  ABPositive: "AB+",
  ABNegative: "AB-",
  OPositive: "O+",
  ONegative: "O-",
};

async function postAgent<T>(
  path: string,
  body: unknown,
): Promise<T> {
  console.log(
    `[Agent API] POST ${AGENT_SERVICE_URL}${path}`,
  );

  console.log(
    "[Agent API] Request body:",
    JSON.stringify(body, null, 2),
  );

  const response = await fetch(
    `${AGENT_SERVICE_URL}${path}`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
    },
  );

  const responseText = await response.text();

  console.log(
    `[Agent API] Response ${response.status}:`,
    responseText,
  );

  if (!response.ok) {
    throw new Error(
      responseText ||
        `Agent service returned ${response.status}`,
    );
  }

  if (!responseText) {
    return {} as T;
  }

  return JSON.parse(responseText) as T;
}

// =====================================================
// STOCK CHECK AGENT
// =====================================================

export interface StockCheckAgentRequest {
  workflowId: string;
  requestingOrgId: string;
  bloodType: string;
  unitsNeeded: number;
}

export interface StockCheckCandidateTransferOrg {
  organizationId: string;
  distanceKm: number;
  unitsAvailable: number;
}

export interface StockCheckAgentResponse {
  sufficient: boolean;
  ownStockUnits: number;
  shortfallUnits: number;
  candidateTransferOrgs: StockCheckCandidateTransferOrg[];
}

export async function runStockCheckAgent(
  request: StockCheckAgentRequest,
): Promise<StockCheckAgentResponse> {
  const payload: StockCheckAgentRequest = {
    workflowId: request.workflowId,
    requestingOrgId: request.requestingOrgId,
    bloodType: request.bloodType,
    unitsNeeded: request.unitsNeeded,
  };

  console.log(
    "========================================",
  );

  console.log(
    "[Stock Check Agent] FINAL REQUEST",
  );

  console.log(
    JSON.stringify(payload, null, 2),
  );

  console.log(
    "========================================",
  );

  return postAgent<StockCheckAgentResponse>(
    "/agents/stock-check",
    payload,
  );
}

// =====================================================
// STOCK RISK AGENT
// =====================================================

export async function runStockRiskAgent(
  organizationId: string,
): Promise<StockRiskResponse> {
  return postAgent<StockRiskResponse>(
    "/agents/stock-risk",
    {
      organizationId,
    },
  );
}

// =====================================================
// EMERGENCY RECOMMENDATION AGENT
// =====================================================

export async function runEmergencyRecommendationAgent(
  request: EmergencyInventoryRequest,
): Promise<EmergencyInventoryRecommendation> {
  return postAgent<EmergencyInventoryRecommendation>(
    "/agents/emergency-recommendation",
    {
      bloodType:
        BLOOD_TYPE_TO_AGENT_VALUE[
          request.bloodType
        ],
      requiredUnits: request.requiredUnits,
      urgency: request.urgency,
    },
  );
}