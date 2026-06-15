using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Data;

namespace WhaleTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/hyperliquid")]
public sealed class HyperliquidLiveController : ControllerBase
{
    private readonly WhaleTrackerDbContext _db;
    private readonly IHyperliquidConsensusService _consensusService;
    private readonly IOkxService _okxService;

    public HyperliquidLiveController(
        WhaleTrackerDbContext db,
        IHyperliquidConsensusService consensusService,
        IOkxService okxService)
    {
        _db = db;
        _consensusService = consensusService;
        _okxService = okxService;
    }

    [HttpGet("live/summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var latestScores = await LatestScores(cancellationToken);
        var enabled = await _db.HyperliquidCopyTraders
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);
        var active = await _db.HyperliquidLivePositions
            .AsNoTracking()
            .Where(x => x.Status == "LIVE_OPEN" || x.Status == "BASELINE_OPEN")
            .ToListAsync(cancellationToken);
        var closed = await _db.HyperliquidLivePositions
            .AsNoTracking()
            .Where(x => x.Status == "CLOSED")
            .ToListAsync(cancellationToken);
        var okx = await SafeOkxAccount(cancellationToken);

        return Ok(new
        {
            trackedTraders = enabled.Count,
            activeTraders = active.Select(x => x.TraderAddress).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            liveOpenPositions = active.Count(x => x.OpenedFromTracking),
            baselineOpenPositions = active.Count(x => !x.OpenedFromTracking),
            closedLivePositions = closed.Count(x => x.OpenedFromTracking),
            sourceRealizedPnlUsd = latestScores.Sum(x => x.RealizedPnlUsd),
            sourceUnrealizedPnlUsd = latestScores.Sum(x => x.UnrealizedPnlUsd),
            pureVirtualPnlUsd = (decimal?)null,
            executableVirtualPnlUsd = (decimal?)null,
            realOkxPnlUsd = (decimal?)null,
            currentOkxEquity = okx?.TotalUsd,
            realExecutionMode = enabled.Any(x => x.ExecuteOrders) ? "Enabled" : "Shadow Only",
            realExecutionTraders = enabled.Count(x => x.ExecuteOrders),
            checkedAt = DateTime.UtcNow
        });
    }

    [HttpGet("live/traders")]
    public async Task<IActionResult> Traders(CancellationToken cancellationToken)
    {
        var traders = await _db.HyperliquidCopyTraders
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Address, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var latestScores = await LatestScores(cancellationToken);
        var generalProfiles = await _db.TraderCoinProfiles
            .AsNoTracking()
            .Where(x => x.Coin == "__GENERAL__")
            .ToDictionaryAsync(x => x.TraderAddress, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var activePositions = await _db.HyperliquidLivePositions
            .AsNoTracking()
            .Where(x => x.Status == "LIVE_OPEN" || x.Status == "BASELINE_OPEN")
            .ToListAsync(cancellationToken);

        var rows = traders.Values
            .OrderByDescending(x => x.IsEnabled)
            .ThenByDescending(x =>
                latestScores.FirstOrDefault(s => s.TraderAddress.Equals(x.Address, StringComparison.OrdinalIgnoreCase))?.LiveScore ?? 0)
            .Select((trader, index) =>
            {
                var score = latestScores.FirstOrDefault(x => x.TraderAddress.Equals(trader.Address, StringComparison.OrdinalIgnoreCase));
                generalProfiles.TryGetValue(trader.Address, out var profile);
                var traderActive = activePositions
                    .Where(x => x.TraderAddress.Equals(trader.Address, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var accountValue = traderActive
                    .Select(x => x.LatestSourceAccountValueUsd > 0 ? x.LatestSourceAccountValueUsd : x.SourceAccountValueAtOpen)
                    .FirstOrDefault(x => x > 0);
                return new
                {
                    rank = index + 1,
                    status = trader.ExecuteOrders ? "Real Enabled" : trader.IsEnabled ? "Shadow" : "Paused",
                    trader.Label,
                    trader.Address,
                    trader.IsEnabled,
                    trader.ExecuteOrders,
                    liveScore = score?.LiveScore,
                    confidence = score?.Confidence,
                    historicalScore = profile?.HistoricalQualityScore,
                    historicalConfidence = profile?.HistoricalConfidenceScore,
                    currentAccountValue = accountValue == 0 ? (decimal?)null : accountValue,
                    liveRealizedPnlUsd = score?.RealizedPnlUsd,
                    liveRealizedPnlPct = score?.PnlPctAccount,
                    pureVirtualPnl = (decimal?)null,
                    executableVirtualPnl = (decimal?)null,
                    realOkxPnl = (decimal?)null,
                    openPositions = traderActive.Count,
                    closedPositions = score?.ClosedPositions ?? 0,
                    wins = score?.Wins,
                    losses = score?.Losses,
                    winrate = score?.WinRate,
                    grossProfitUsd = (decimal?)null,
                    grossLossUsd = (decimal?)null,
                    profitFactor = profile?.ProfitFactor,
                    avgHoldSeconds = score?.AvgHoldSeconds ?? profile?.AvgHoldSeconds,
                    medianHoldSeconds = profile?.MedianHoldSeconds,
                    bestTrade = score?.BestTradeUsd ?? profile?.BestTradePnlUsd,
                    worstTrade = score?.WorstTradeUsd ?? profile?.WorstTradePnlUsd,
                    maxDrawdown = (decimal?)null,
                    okxCopyablePnl = profile?.NetPnlUsd,
                    minOrderRejectRate = (decimal?)null,
                    conflictRate = (decimal?)null,
                    lastSignalAt = trader.LastSyncAt ?? trader.LastFillPollAt,
                    topCoins = profile == null ? string.Empty : awaitTopCoinsPlaceholder(profile.TraderAddress)
                };
            })
            .ToList();

        return Ok(rows);

        string awaitTopCoinsPlaceholder(string address)
        {
            var top = _db.TraderCoinProfiles
                .AsNoTracking()
                .Where(x => x.TraderAddress == address && x.Coin != "__GENERAL__")
                .OrderByDescending(x => x.NetPnlUsd)
                .Take(5)
                .Select(x => $"{x.Coin}:{x.NetPnlUsd:0}")
                .ToList();
            return string.Join("; ", top);
        }
    }

    [HttpGet("live/traders/{address}")]
    public async Task<IActionResult> Trader(string address, CancellationToken cancellationToken)
    {
        var normalized = NormalizeAddress(address);
        var trader = await _db.HyperliquidCopyTraders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Address == normalized, cancellationToken);
        if (trader == null)
        {
            return NotFound();
        }

        var score = (await LatestScores(cancellationToken))
            .FirstOrDefault(x => x.TraderAddress.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        var general = await _db.TraderCoinProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TraderAddress == normalized && x.Coin == "__GENERAL__", cancellationToken);
        var active = await TraderPositions(normalized, true, cancellationToken);
        var closed = await TraderPositions(normalized, false, cancellationToken);
        var accountValue = active
            .Select(x => x.LatestSourceAccountValueUsd > 0 ? x.LatestSourceAccountValueUsd : x.SourceAccountValueAtOpen)
            .FirstOrDefault(x => x > 0);

        return Ok(new
        {
            trader.Address,
            trader.Label,
            status = trader.ExecuteOrders ? "Real Enabled" : trader.IsEnabled ? "Shadow" : "Paused",
            trader.IsEnabled,
            trader.ExecuteOrders,
            historicalScore = general?.HistoricalQualityScore,
            liveScore = score?.LiveScore,
            confidence = score?.Confidence ?? general?.HistoricalConfidenceScore,
            currentAccountValue = accountValue == 0 ? (decimal?)null : accountValue,
            currentWithdrawable = (decimal?)null,
            currentMarginUsed = (decimal?)null,
            totalPositionNotional = active.Sum(x => x.CurrentNotionalUsd),
            activePositionCount = active.Count,
            lastSeen = active.Select(x => (DateTime?)x.LastSeenAt).OrderByDescending(x => x).FirstOrDefault() ?? trader.LastSyncAt,
            trackingStartDate = trader.CreatedAt,
            metrics = new
            {
                realized30dPnlUsd = general?.NetPnlUsd,
                realized30dPnlPct = (decimal?)null,
                liveRealizedPnlUsd = score?.RealizedPnlUsd,
                liveRealizedPnlPct = score?.PnlPctAccount,
                totalClosedPositions = general?.ClosedPositions,
                winningPositions = general?.WinningPositions,
                losingPositions = general?.LosingPositions,
                winrate = general?.WinRate ?? score?.WinRate,
                grossProfitUsd = general?.GrossProfitUsd,
                grossLossUsd = general?.GrossLossUsd,
                profitFactor = general?.ProfitFactor,
                averageHoldSeconds = general?.AvgHoldSeconds,
                medianHoldSeconds = general?.MedianHoldSeconds,
                bestTrade = general?.BestTradePnlUsd ?? score?.BestTradeUsd,
                worstTrade = general?.WorstTradePnlUsd ?? score?.WorstTradeUsd,
                maxDrawdown = (decimal?)null,
                okxCopyablePnl = general?.NetPnlUsd,
                pureVirtualCopyPnl = (decimal?)null,
                executableVirtualCopyPnl = (decimal?)null,
                realOkxCopiedPnl = (decimal?)null,
                minOrderRejectRate = (decimal?)null,
                skippedSignalCount = score?.SkippedPositions
            }
        });
    }

    [HttpGet("live/traders/{address}/active-positions")]
    public async Task<IActionResult> TraderActivePositions(string address, CancellationToken cancellationToken) =>
        Ok((await TraderPositions(NormalizeAddress(address), true, cancellationToken)).Select(MapPosition));

    [HttpGet("live/traders/{address}/closed-positions")]
    public async Task<IActionResult> TraderClosedPositions(string address, CancellationToken cancellationToken) =>
        Ok((await TraderPositions(NormalizeAddress(address), false, cancellationToken)).Select(MapPosition));

    [HttpGet("live/traders/{address}/coin-performance")]
    public async Task<IActionResult> CoinPerformance(string address, CancellationToken cancellationToken)
    {
        var normalized = NormalizeAddress(address);
        var rows = await _db.TraderCoinProfiles
            .AsNoTracking()
            .Where(x => x.TraderAddress == normalized && x.Coin != "__GENERAL__")
            .OrderByDescending(x => x.CoinSkillScore)
            .ThenByDescending(x => x.NetPnlUsd)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new
        {
            x.Coin,
            x.ClosedPositions,
            x.WinningPositions,
            x.LosingPositions,
            x.WinRate,
            x.NetPnlUsd,
            x.GrossProfitUsd,
            x.GrossLossUsd,
            x.ProfitFactor,
            x.AvgAllocPct,
            x.MedianAllocPct,
            x.P75AllocPct,
            x.P90AllocPct,
            x.MaxAllocPct,
            x.AvgHoldSeconds,
            x.BestTradePnlUsd,
            x.WorstTradePnlUsd,
            x.CoinSkillScore,
            x.SampleConfidence
        }));
    }

    [HttpGet("live/traders/{address}/allocation-profile")]
    public async Task<IActionResult> AllocationProfile(string address, CancellationToken cancellationToken)
    {
        var normalized = NormalizeAddress(address);
        var profiles = await _db.TraderCoinProfiles.AsNoTracking()
            .Where(x => x.TraderAddress == normalized && x.Coin != "__GENERAL__")
            .ToListAsync(cancellationToken);
        var exposures = await _db.TraderCoinCurrentExposures.AsNoTracking()
            .Where(x => x.TraderAddress == normalized)
            .ToDictionaryAsync(x => x.Coin, StringComparer.OrdinalIgnoreCase, cancellationToken);
        return Ok(profiles.OrderByDescending(x => x.CoinSkillScore).Select(x =>
        {
            exposures.TryGetValue(x.Coin, out var exposure);
            return new
            {
                x.Coin,
                minAllocPct = (decimal?)null,
                p25AllocPct = (decimal?)null,
                x.MedianAllocPct,
                x.AvgAllocPct,
                x.P75AllocPct,
                x.P90AllocPct,
                x.MaxAllocPct,
                currentAllocPct = exposure?.CurrentAllocPct,
                currentVsMedian = exposure == null || x.MedianAllocPct <= 0 ? (decimal?)null : exposure.CurrentAllocPct / x.MedianAllocPct,
                currentVsP90 = exposure == null || x.P90AllocPct <= 0 ? (decimal?)null : exposure.CurrentAllocPct / x.P90AllocPct,
                allocationConviction = exposure?.AllocationConviction
            };
        }));
    }

    [HttpGet("live/traders/{address}/copy-simulation")]
    public IActionResult CopySimulation(string address) => Ok(Array.Empty<object>());

    [HttpGet("live/traders/{address}/okx-orders")]
    public IActionResult OkxOrders(string address) => Ok(Array.Empty<object>());

    [HttpGet("live/traders/{address}/raw-events")]
    public async Task<IActionResult> RawEvents(string address, CancellationToken cancellationToken)
    {
        var normalized = NormalizeAddress(address);
        var fills = await _db.HyperliquidLiveFills.AsNoTracking()
            .Where(x => x.TraderAddress == normalized)
            .OrderByDescending(x => x.ExchangeTime)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Ok(fills);
    }

    [HttpGet("consensus")]
    public async Task<IActionResult> Consensus(CancellationToken cancellationToken) =>
        Ok(await _consensusService.GetSnapshotAsync(cancellationToken));

    [HttpGet("consensus/{coin}/contributors")]
    public async Task<IActionResult> ConsensusContributors(string coin, CancellationToken cancellationToken)
    {
        var normalized = coin.ToUpperInvariant();
        var rows = await _db.TraderCoinCurrentExposures.AsNoTracking()
            .Where(x => x.Coin == normalized)
            .OrderByDescending(x => Math.Abs(x.WeightedSignal))
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpGet("positions/active")]
    public async Task<IActionResult> ActivePositions(CancellationToken cancellationToken)
    {
        var rows = await _db.HyperliquidLivePositions.AsNoTracking()
            .Where(x => x.Status == "LIVE_OPEN" || x.Status == "BASELINE_OPEN")
            .OrderByDescending(x => x.LastSeenAt)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(MapPosition));
    }

    [HttpGet("positions/closed")]
    public async Task<IActionResult> ClosedPositions(CancellationToken cancellationToken)
    {
        var rows = await _db.HyperliquidLivePositions.AsNoTracking()
            .Where(x => x.Status == "CLOSED")
            .OrderByDescending(x => x.ClosedAt ?? x.UpdatedAt)
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(MapPosition));
    }

    [HttpGet("execution/summary")]
    public async Task<IActionResult> ExecutionSummary(CancellationToken cancellationToken)
    {
        var okx = await SafeOkxAccount(cancellationToken);
        var traders = await _db.HyperliquidCopyTraders.AsNoTracking()
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);
        var consensus = await _consensusService.GetSnapshotAsync(cancellationToken);
        return Ok(new
        {
            okxEquity = okx?.TotalUsd,
            realExecutionMode = traders.Any(x => x.ExecuteOrders) ? "Enabled" : "Shadow Only",
            realExecutionTraders = traders.Count(x => x.ExecuteOrders),
            openOkxPositions = okx?.ActivePositions ?? new List<Core.Models.Position>(),
            targetExposurePerCoin = consensus.Coins.Select(x => new
            {
                x.Coin,
                x.TargetSide,
                x.TargetNotionalUsd,
                x.Action,
                x.SkipReason
            }),
            riskCaps = new
            {
                maxRealMarginUsedUsd = 25,
                maxGrossNotionalUsd = 300,
                maxCoinNotionalUsd = 80
            }
        });
    }

    [HttpGet("execution/orders")]
    public async Task<IActionResult> ExecutionOrders(CancellationToken cancellationToken)
    {
        var rows = await _db.HyperliquidCopyEvents.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpGet("execution/positions")]
    public async Task<IActionResult> ExecutionPositions(CancellationToken cancellationToken)
    {
        var okx = await SafeOkxAccount(cancellationToken);
        return Ok(okx?.ActivePositions ?? new List<Core.Models.Position>());
    }

    [HttpPost("traders/{address}/pause")]
    public async Task<IActionResult> PauseTrader(string address, CancellationToken cancellationToken)
    {
        var trader = await _db.HyperliquidCopyTraders
            .FirstOrDefaultAsync(x => x.Address == NormalizeAddress(address), cancellationToken);
        if (trader == null) return NotFound();
        trader.IsEnabled = false;
        trader.ExecuteOrders = false;
        trader.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost("traders/{address}/resume")]
    public async Task<IActionResult> ResumeTrader(string address, CancellationToken cancellationToken)
    {
        var trader = await _db.HyperliquidCopyTraders
            .FirstOrDefaultAsync(x => x.Address == NormalizeAddress(address), cancellationToken);
        if (trader == null) return NotFound();
        trader.IsEnabled = true;
        trader.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    [HttpPost("execution/disable-real")]
    public async Task<IActionResult> DisableReal(CancellationToken cancellationToken)
    {
        await _db.HyperliquidCopyTraders.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.ExecuteOrders, false).SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken);
        return Ok(new { success = true, mode = "Shadow Only" });
    }

    [HttpPost("execution/enable-shadow-only")]
    public Task<IActionResult> EnableShadowOnly(CancellationToken cancellationToken) => DisableReal(cancellationToken);

    private async Task<List<Data.Entities.HyperliquidLiveScoreSnapshotEntity>> LatestScores(CancellationToken cancellationToken) =>
        (await _db.HyperliquidLiveScoreSnapshots
                .AsNoTracking()
                .OrderByDescending(x => x.ScoredAt)
                .ThenByDescending(x => x.Id)
                .Take(2000)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.TraderAddress, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

    private async Task<List<Data.Entities.HyperliquidLivePositionEntity>> TraderPositions(
        string address,
        bool active,
        CancellationToken cancellationToken)
    {
        var query = _db.HyperliquidLivePositions.AsNoTracking()
            .Where(x => x.TraderAddress == address);
        query = active
            ? query.Where(x => x.Status == "LIVE_OPEN" || x.Status == "BASELINE_OPEN")
            : query.Where(x => x.Status == "CLOSED");
        return await query
            .OrderByDescending(x => active ? x.LastSeenAt : x.ClosedAt ?? x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    private static object MapPosition(Data.Entities.HyperliquidLivePositionEntity x)
    {
        var account = x.LatestSourceAccountValueUsd > 0 ? x.LatestSourceAccountValueUsd : x.SourceAccountValueAtOpen;
        return new
        {
            x.Id,
            x.TraderAddress,
            x.Coin,
            x.OkxSymbol,
            x.Side,
            x.Status,
            x.OpenedAt,
            x.FirstSeenAt,
            x.LastSeenAt,
            x.ClosedAt,
            x.EntryPrice,
            x.ExitPrice,
            currentMarkPrice = (decimal?)null,
            x.CurrentSize,
            x.MaxSize,
            x.CurrentNotionalUsd,
            x.MaxNotionalUsd,
            sourceAccountValueAtOpen = x.SourceAccountValueAtOpen,
            latestSourceAccountValueUsd = x.LatestSourceAccountValueUsd,
            allocPctOfAccount = account <= 0 ? (decimal?)null : x.CurrentNotionalUsd / account * 100m,
            marginMode = "unknown",
            leverage = (decimal?)null,
            x.UnrealizedPnlUsd,
            unrealizedPnlPct = x.CurrentNotionalUsd <= 0 ? (decimal?)null : x.UnrealizedPnlUsd / x.CurrentNotionalUsd * 100m,
            x.RealizedPnlUsd,
            realizedPnlPct = x.MaxNotionalUsd <= 0 ? (decimal?)null : x.RealizedPnlUsd / x.MaxNotionalUsd * 100m,
            pnlPctNotional = x.MaxNotionalUsd <= 0 ? (decimal?)null : x.NetPnlUsd / x.MaxNotionalUsd * 100m,
            pnlPctAccount = account <= 0 ? (decimal?)null : x.NetPnlUsd / account * 100m,
            fees = x.FeeUsd,
            funding = (decimal?)null,
            x.NetPnlUsd,
            x.OpenedFromTracking,
            x.IsOkxTradable,
            copiedReal = x.CopyStatus.Contains("cop", StringComparison.OrdinalIgnoreCase),
            x.CopyStatus,
            x.SkipReason,
            reconstructionQuality = "stored_live_state"
        };
    }

    private async Task<Core.Models.UserStats?> SafeOkxAccount(CancellationToken cancellationToken)
    {
        try
        {
            return await _okxService.GetAccountInfoAsync();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeAddress(string value) => value.Trim().ToLowerInvariant();
}
