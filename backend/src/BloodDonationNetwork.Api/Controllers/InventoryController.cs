using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using BloodDonationNetwork.Application.DTOs.Inventory;
using BloodDonationNetwork.Application.Interfaces;
using BloodDonationNetwork.Domain.Entities;
using BloodDonationNetwork.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodDonationNetwork.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "staff,admin")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IApplicationDbContext _db;

    public InventoryController(
        IInventoryService inventoryService,
        IApplicationDbContext db)
    {
        _inventoryService = inventoryService;
        _db = db;
    }

    // =====================================================
    // NORMAL AUTHENTICATED INVENTORY ENDPOINTS
    // =====================================================

    [HttpGet("{organizationId:guid}")]
    public async Task<ActionResult<IReadOnlyList<InventoryResponse>>> GetInventory(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetInventoryAsync(
            organizationId,
            GetCurrentUserId(),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyList<InventoryResponse>>> GetLowStock(
        [FromQuery] Guid organizationId,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetLowStockAsync(
            organizationId,
            GetCurrentUserId(),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("stock-risk/{organizationId:guid}")]
    public async Task<ActionResult<StockRiskResponse>> GetStockRisk(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryService.AnalyzeStockRiskAsync(
            organizationId,
            GetCurrentUserId(),
            cancellationToken);

        return Ok(result);
    }

    [HttpPut("{id:guid}/adjust")]
    public async Task<ActionResult<InventoryResponse>> AdjustInventory(
        Guid id,
        [FromBody] AdjustInventoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inventoryService.AdjustInventoryAsync(
                id,
                request,
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("transactions")]
    public async Task<ActionResult<InventoryTransactionResponse>> CreateTransaction(
        [FromBody] CreateInventoryTransactionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inventoryService.CreateTransactionAsync(
                request,
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("transactions/{organizationId:guid}")]
    public async Task<ActionResult> GetTransactions(
        Guid organizationId,
        [FromQuery] InventoryTransactionType? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _inventoryService.GetTransactionsAsync(
            organizationId,
            GetCurrentUserId(),
            type,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    // =====================================================
    // FRONTEND EMERGENCY / AGENT REQUESTS
    //
    // These endpoints are NORMAL authenticated API routes.
    // They are intentionally NOT under /api/internal.
    //
    // React can safely call these routes with the user's
    // normal authentication token.
    // =====================================================

    [HttpPost("stock-check")]
    public async Task<ActionResult<StockCheckResponse>> FrontendStockCheck(
        [FromBody] StockCheckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inventoryService.CheckStockAsync(
                request,
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // React's Emergency & Risk screen needs the transfer-candidate list
    // (nearby orgs with spare stock) that FrontendStockCheck's plain
    // StockCheckResponse doesn't carry. That richer shape previously
    // only existed behind /api/internal/agent/check-stock, which the
    // frontend had no business calling directly — it has no way to
    // present the internal secret, so it was calling the agent-service's
    // own /agents/stock-check with no credentials at all instead. This
    // gives the browser the same data through a normal, JWT-authenticated,
    // org-scoped route instead.
    [HttpPost("stock-check/candidates")]
    public async Task<ActionResult<StockCheckAgentResponse>> FrontendStockCheckWithCandidates(
        [FromBody] StockCheckRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _inventoryService.CheckStockWithTransferCandidatesAsync(
                request,
                GetCurrentUserId(),
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("emergency-recommendation")]
    public async Task<ActionResult<EmergencyInventoryRecommendation>>
        FrontendEmergencyRecommendation(
            [FromBody] EmergencyInventoryRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _inventoryService.GetEmergencyRecommendationAsync(
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =====================================================
    // INTERNAL AI AGENT ENDPOINTS
    //
    // DO NOT REMOVE THESE.
    //
    // These routes are protected by the internal-secret
    // middleware and are intended for server-to-server
    // communication.
    // =====================================================

    [HttpPost("/api/internal/agent/check-stock")]
    [AllowAnonymous]
    public async Task<ActionResult<StockCheckAgentResponse>> CheckStock(
        [FromBody] StockCheckAgentRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;

        try
        {
            var result = await _inventoryService.CheckStockForAgentAsync(
                request,
                cancellationToken);

            await LogStockCheckStepAsync(
                request.WorkflowId,
                request,
                result,
                "completed",
                startedAt,
                null,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            await LogStockCheckStepAsync(
                request.WorkflowId,
                request,
                null,
                "failed",
                startedAt,
                ex.Message,
                CancellationToken.None);

            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            await LogStockCheckStepAsync(
                request.WorkflowId,
                request,
                null,
                "failed",
                startedAt,
                ex.Message,
                CancellationToken.None);

            return NotFound(new { message = ex.Message });
        }
    }

    private async Task LogStockCheckStepAsync(
        Guid workflowId,
        StockCheckAgentRequest request,
        StockCheckAgentResponse? response,
        string status,
        DateTime startedAt,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var workflow = await _db.AgentWorkflows
            .FirstOrDefaultAsync(
                x => x.Id == workflowId,
                cancellationToken);

        if (workflow is null)
        {
            return;
        }

        workflow.CurrentAgent = AgentNames.StockCheck;
        workflow.UpdatedAt = DateTime.UtcNow;

        _db.AgentSteps.Add(new AgentStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            AgentName = AgentNames.StockCheck,
            StepName = "stock_check",
            Status = status,
            InputJson = JsonSerializer.Serialize(request),
            OutputJson = response is null
                ? null
                : JsonSerializer.Serialize(response),
            Narrative = response is null
                ? "Stock check failed."
                : response.Sufficient
                    ? "The requesting organization has sufficient stock."
                    : $"Stock is short by {response.ShortfallUnits} unit(s); transfer candidates were evaluated.",
            RetryCount = 0,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage,
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    [HttpGet("/api/internal/agent/stock-risk/{organizationId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<StockRiskResponse>> AnalyzeStockRisk(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var result =
            await _inventoryService.AnalyzeStockRiskForAgentAsync(
                organizationId,
                cancellationToken);

        return Ok(result);
    }

    [HttpPost("/api/internal/agent/inventory-recommendation")]
    [AllowAnonymous]
    public async Task<ActionResult<EmergencyInventoryRecommendation>>
        GetEmergencyRecommendation(
            [FromBody] EmergencyInventoryRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await _inventoryService.GetEmergencyRecommendationAsync(
                    request,
                    cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =====================================================
    // CURRENT USER
    // =====================================================

    private Guid GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var currentUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user ID is missing or invalid.");
        }

        return currentUserId;
    }
}