using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;
using WhaleTracker.Data;

namespace WhaleTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/operations")]
public class OperationsController : ControllerBase
{
    private readonly WhaleTrackerDbContext _db;
    private readonly IOkxService _okxService;
    private readonly AppSettings _settings;

    public OperationsController(
        WhaleTrackerDbContext db,
        IOkxService okxService,
        IOptions<AppSettings> settings)
    {
        _db = db;
        _okxService = okxService;
        _settings = settings.Value;
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> Snapshot(CancellationToken cancellationToken = default)
    {
        var runtime = await _db.RuntimeControls
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == "global", cancellationToken);

        var aiState = await _db.AiBiasStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == "global", cancellationToken);

        var recentTrades = await _db.TradeLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.CreatedAt,
                x.WhaleTxHash,
                x.OkxOrderId,
                x.Symbol,
                x.Action,
                x.MarginUsdt,
                x.Leverage,
                x.ExecutedPrice,
                x.IsSuccess,
                x.ErrorMessage,
                x.Confidence,
                x.AiReason
            })
            .ToListAsync(cancellationToken);

        var recentAiEvents = await _db.AiDecisionEvents
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .Select(x => new
            {
                x.Id,
                x.CreatedAt,
                x.TxHash,
                x.WalletAddress,
                x.MovementType,
                x.Symbol,
                x.MovementUsd,
                x.Action,
                x.ShouldTrade,
                x.BiasDelta,
                x.BiasScoreAfter,
                x.IgnoredReason
            })
            .ToListAsync(cancellationToken);

        object okx;
        try
        {
            var account = await _okxService.GetAccountInfoAsync();
            okx = new
            {
                ok = true,
                isDemo = _settings.Okx.IsDemo,
                totalUsd = account.TotalUsd,
                leverage = account.Leverage,
                positions = account.ActivePositions.Select(x => new
                {
                    x.Symbol,
                    x.Direction,
                    x.MarginUsd,
                    x.EntryPrice,
                    x.Size,
                    x.UnrealizedPnl
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            okx = new
            {
                ok = false,
                isDemo = _settings.Okx.IsDemo,
                error = ex.Message,
                totalUsd = 0,
                positions = Array.Empty<object>()
            };
        }

        return Ok(new
        {
            checkedAt = DateTime.UtcNow,
            okx,
            runtime = new
            {
                id = runtime?.Id ?? "global",
                autoTradingEnabled = runtime?.AutoTradingEnabled ?? false,
                pollingIntervalSeconds = runtime?.PollingIntervalSeconds ?? 30,
                lastWorkerHeartbeatAt = runtime?.LastWorkerHeartbeatAt,
                lastScanStartedAt = runtime?.LastScanStartedAt,
                lastScanCompletedAt = runtime?.LastScanCompletedAt,
                lastError = runtime?.LastError ?? string.Empty,
                updatedAt = runtime?.UpdatedAt
            },
            aiState = new
            {
                id = aiState?.Id ?? "global",
                biasScore = aiState?.BiasScore ?? 0m,
                direction = aiState?.Direction ?? "NEUTRAL",
                summary = aiState?.Summary ?? "No AI memory events recorded yet.",
                eventCount = aiState?.EventCount ?? 0,
                lastEventAt = aiState?.LastEventAt,
                updatedAt = aiState?.UpdatedAt
            },
            recentTrades,
            recentAiEvents
        });
    }
}
