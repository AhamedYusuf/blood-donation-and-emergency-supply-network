import type {
  EmergencyInventoryRecommendation,
  EmergencyInventoryRequest,
  StockCheckRequest,
  StockCheckResponse,
  StockRiskResponse,
  BloodType,
} from "./inventoryTypes";

const AGENT_SERVICE_URL =
  import.meta.env.VITE_AGENT_SERVICE_URL ||
  "http://localhost:8000";

/**
 * Convert the frontend BloodType enum/value
 * into the format expected by the Python agents.
 */
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

  if (!response.ok) {
    const text = await response.text();

    throw new Error(
      text || `Agent service returned ${response.status}`,
    );
  }

  return response.json() as Promise<T>;
}

// =====================================================
// AGENT 01 - STOCK CHECK
// =====================================================

export async function runStockCheckAgent(
  request: StockCheckRequest,
): Promise<StockCheckResponse> {
  return postAgent<StockCheckResponse>(
    "/agents/stock-check",
    {
      organizationId: request.organizationId,
      bloodType:
        BLOOD_TYPE_TO_AGENT_VALUE[request.bloodType],
      requiredUnits: request.requiredUnits,
    },
  );
}

// =====================================================
// AGENT 02 - STOCK RISK
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
// AGENT 03 - EMERGENCY RECOMMENDATION
// =====================================================

export async function runEmergencyRecommendationAgent(
  request: EmergencyInventoryRequest,
): Promise<EmergencyInventoryRecommendation> {
  return postAgent<EmergencyInventoryRecommendation>(
    "/agents/emergency-recommendation",
    {
      bloodType:
        BLOOD_TYPE_TO_AGENT_VALUE[request.bloodType],
      requiredUnits: request.requiredUnits,
      urgency: request.urgency,
    },
  );
}