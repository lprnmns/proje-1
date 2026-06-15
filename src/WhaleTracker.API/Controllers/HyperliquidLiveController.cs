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
    private const decimal ShadowConsensusThreshold = 15m;
    private const decimal ShadowConsensusMultiplier = 0.75m;
    private const decimal ShadowMinOrderNotionalUsd = 6.85m;
    private const decimal ShadowMinRebalanceNotionalUsd = 8m;
    private const decimal ShadowLeverage = 2m;
    private const decimal ShadowMaxCoinMarginPct = 0.15m;
    private const decimal ShadowMaxTotalMarginPct = 0.35m;

    private readonly WhaleTrackerDbContext _db;
    private readonly IHyperliquidConsensusService _consensusService;
    private readonly IHyperliquidConsensusExecutionService _executionService;
    private readonly IOkxService _okxService;

    public HyperliquidLiveController(
        WhaleTrackerDbContext db,
        IHyperliquidConsensusService consensusService,
        IHyperliquidConsensusExecutionService executionService,
        IOkxService okxService)
    {
        _db = db;
        _consensusService = consensusService;
        _executionService = executionService;
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
        var plan = await _executionService.BuildPlanAsync(cancellationToken);
        var consensusMode = plan.Config.WorkerEnabled ? "Consensus Enabled" : "Shadow Only";
        var perTraderMode = enabled.Any(x => x.ExecuteOrders) ? "Per-Trader Enabled" : consensusMode;

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
            realExecutionMode = perTraderMode,
            realExecutionTraders = enabled.Count(x => x.ExecuteOrders),
            consensusExecutionEnabled = plan.Config.WorkerEnabled,
            consensusExecutionConfig = plan.Config,
            trackingStartedAt = enabled.Select(x => (DateTime?)x.CreatedAt).OrderBy(x => x).FirstOrDefault(),
            latestScoreAt = latestScores.Select(x => (DateTime?)x.ScoredAt).OrderByDescending(x => x).FirstOrDefault(),
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
        var profiles = await _db.TraderCoinSideProfiles.AsNoTracking()
            .Where(x => x.TraderAddress == normalized)
            .ToListAsync(cancellationToken);
        var exposures = (await _db.TraderCoinCurrentExposures.AsNoTracking()
            .Where(x => x.TraderAddress == normalized)
            .ToListAsync(cancellationToken))
            .ToDictionary(x => (x.Coin.ToUpperInvariant(), x.Side.ToUpperInvariant()));
        return Ok(profiles.OrderByDescending(x => x.CoinSideSkillScore).Select(x =>
        {
            exposures.TryGetValue((x.Coin.ToUpperInvariant(), x.Side.ToUpperInvariant()), out var exposure);
            return new
            {
                x.Coin,
                x.Side,
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
                allocationConviction = exposure?.AllocationConviction,
                coinSideSkillScore = x.CoinSideSkillScore,
                x.SampleConfidence
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
        var profiles = (await _db.TraderCoinSideProfiles.AsNoTracking()
            .Where(x => x.Coin == normalized)
            .ToListAsync(cancellationToken))
            .ToDictionary(x => (
                x.TraderAddress.ToLowerInvariant(),
                x.Coin.ToUpperInvariant(),
                x.Side.ToUpperInvariant()));
        var rowAddresses = rows.Select(row => row.TraderAddress).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var labels = await _db.HyperliquidCopyTraders.AsNoTracking()
            .Where(x => rowAddresses.Contains(x.Address))
            .ToDictionaryAsync(x => x.Address, x => x.Label, StringComparer.OrdinalIgnoreCase, cancellationToken);
        return Ok(rows.Select(row =>
        {
            profiles.TryGetValue((
                row.TraderAddress.ToLowerInvariant(),
                row.Coin.ToUpperInvariant(),
                row.Side.ToUpperInvariant()), out var profile);
            labels.TryGetValue(row.TraderAddress, out var label);
            return new
            {
                row.TraderAddress,
                label = label ?? string.Empty,
                row.Coin,
                row.Side,
                row.CurrentNotionalUsd,
                row.CurrentAccountValueUsd,
                row.CurrentAllocPct,
                row.UnrealizedPnlUsd,
                row.EntryPrice,
                row.OpenedAt,
                row.LastSeenAt,
                row.NormalizedExposure,
                row.AllocationConviction,
                row.CoinSkillScore,
                row.SampleConfidence,
                row.FreshnessScore,
                row.RiskAdjustment,
                row.WeightedSignal,
                row.IsBaseline,
                historicalMedianAllocPct = profile?.MedianAllocPct,
                historicalP75AllocPct = profile?.P75AllocPct,
                historicalP90AllocPct = profile?.P90AllocPct,
                currentVsMedian = profile == null || profile.MedianAllocPct <= 0 ? (decimal?)null : row.CurrentAllocPct / profile.MedianAllocPct,
                currentVsP90 = profile == null || profile.P90AllocPct <= 0 ? (decimal?)null : row.CurrentAllocPct / profile.P90AllocPct,
                coinSideSkillScore = profile?.CoinSideSkillScore,
                sideSampleConfidence = profile?.SampleConfidence,
                sideClosedPositions = profile?.ClosedPositions
            };
        }));
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
        var okxPositions = okx?.ActivePositions ?? new List<Core.Models.Position>();
        var plan = await _executionService.BuildPlanAsync(cancellationToken);
        return Ok(new
        {
            okxEquity = okx?.TotalUsd,
            realExecutionMode = plan.Config.WorkerEnabled ? "Consensus Enabled" : traders.Any(x => x.ExecuteOrders) ? "Per-Trader Enabled" : "Shadow Only",
            realExecutionTraders = traders.Count(x => x.ExecuteOrders),
            openOkxPositions = okxPositions,
            shadowExecutionPlan = plan,
            targetExposurePerCoin = plan.Rows,
            riskCaps = new
            {
                leverage = ShadowLeverage,
                maxCoinMarginPct = ShadowMaxCoinMarginPct,
                maxTotalMarginPct = ShadowMaxTotalMarginPct,
                maxCoinNotionalUsd = plan.Config.MaxCoinNotionalUsd,
                maxGrossNotionalUsd = plan.Config.MaxTotalNotionalUsd,
                minOrderNotionalUsd = ShadowMinOrderNotionalUsd,
                minRebalanceNotionalUsd = ShadowMinRebalanceNotionalUsd
            }
        });
    }

    [HttpGet("execution/plan")]
    public async Task<IActionResult> ExecutionPlan(CancellationToken cancellationToken)
    {
        return Ok(await _executionService.BuildPlanAsync(cancellationToken));
    }

    [HttpPost("execution/apply-plan")]
    public async Task<IActionResult> ApplyExecutionPlan(CancellationToken cancellationToken)
    {
        return Ok(await _executionService.ApplyPlanAsync(cancellationToken));
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

    private static ShadowExecutionPlan BuildShadowExecutionPlan(
        IReadOnlyList<Core.Models.CoinConsensusView> coins,
        IReadOnlyList<Core.Models.Position> okxPositions,
        decimal equityUsd)
    {
        var equity = equityUsd > 0 ? equityUsd : 100m;
        var maxCoinNotional = equity * ShadowLeverage * ShadowMaxCoinMarginPct;
        var maxTotalNotional = equity * ShadowLeverage * ShadowMaxTotalMarginPct;

        var rawRows = coins
            .Select(coin =>
            {
                var signedRaw = Math.Abs(coin.DirectionScore) < ShadowConsensusThreshold
                    ? 0m
                    : coin.DirectionScore * ShadowConsensusMultiplier;
                var signedCapped = Math.Clamp(signedRaw, -maxCoinNotional, maxCoinNotional);
                if (Math.Abs(signedCapped) < ShadowMinOrderNotionalUsd)
                {
                    signedCapped = 0m;
                }

                return new ShadowTargetDraft(
                    coin.Coin,
                    coin.DirectionScore,
                    coin.QualityScore,
                    coin.ConflictRatio,
                    coin.Participation,
                    coin.ContributorCount,
                    signedRaw,
                    signedCapped);
            })
            .ToList();

        var gross = rawRows.Sum(x => Math.Abs(x.SignedTargetNotionalUsd));
        var scale = gross > maxTotalNotional && maxTotalNotional > 0 ? maxTotalNotional / gross : 1m;
        var rows = rawRows
            .Select(row =>
            {
                var signedTarget = row.SignedTargetNotionalUsd * scale;
                if (Math.Abs(signedTarget) < ShadowMinOrderNotionalUsd)
                {
                    signedTarget = 0m;
                }

                var current = EstimatedSignedOkxNotional(okxPositions, row.Coin, ShadowLeverage);
                var delta = signedTarget - current;
                var action = ShadowAction(current, signedTarget, delta);
                var reason = ShadowReason(row.DirectionScore, signedTarget, delta, action);
                return new ShadowExecutionPlanRow
                {
                    Coin = row.Coin,
                    DirectionScore = row.DirectionScore,
                    QualityScore = row.QualityScore,
                    ConflictRatio = row.ConflictRatio,
                    Participation = row.Participation,
                    ContributorCount = row.ContributorCount,
                    TargetSide = signedTarget > 0 ? "LONG" : signedTarget < 0 ? "SHORT" : "FLAT",
                    RawTargetNotionalUsd = row.SignedRawNotionalUsd,
                    SignedTargetNotionalUsd = signedTarget,
                    TargetNotionalUsd = Math.Abs(signedTarget),
                    TargetMarginUsd = Math.Abs(signedTarget) / ShadowLeverage,
                    CurrentOkxNotionalUsd = current,
                    DeltaNotionalUsd = delta,
                    Action = action,
                    SkipReason = reason
                };
            })
            .OrderByDescending(x => Math.Abs(x.DeltaNotionalUsd))
            .ThenByDescending(x => Math.Abs(x.SignedTargetNotionalUsd))
            .ToList();

        var activeTargets = rows.Where(x => Math.Abs(x.SignedTargetNotionalUsd) > 0).ToList();
        return new ShadowExecutionPlan
        {
            Mode = "Shadow Only",
            Config = new ShadowExecutionPlanConfig
            {
                Threshold = ShadowConsensusThreshold,
                Multiplier = ShadowConsensusMultiplier,
                MinOrderNotionalUsd = ShadowMinOrderNotionalUsd,
                MinRebalanceNotionalUsd = ShadowMinRebalanceNotionalUsd,
                Leverage = ShadowLeverage,
                MarginMode = "isolated",
                MaxCoinMarginPct = ShadowMaxCoinMarginPct,
                MaxTotalMarginPct = ShadowMaxTotalMarginPct,
                EquityUsd = equity,
                MaxCoinNotionalUsd = maxCoinNotional,
                MaxTotalNotionalUsd = maxTotalNotional,
                TotalScaleApplied = scale
            },
            Summary = new ShadowExecutionPlanSummary
            {
                ActiveTargetCoins = activeTargets.Count,
                GrossTargetNotionalUsd = activeTargets.Sum(x => Math.Abs(x.SignedTargetNotionalUsd)),
                GrossTargetMarginUsd = activeTargets.Sum(x => Math.Abs(x.TargetMarginUsd)),
                OpenActions = rows.Count(x => x.Action is "OPEN_LONG" or "OPEN_SHORT"),
                CloseActions = rows.Count(x => x.Action == "CLOSE"),
                RebalanceActions = rows.Count(x => x.Action is "INCREASE" or "REDUCE" or "FLIP"),
                HoldActions = rows.Count(x => x.Action == "HOLD")
            },
            Rows = rows
        };
    }

    private static string ShadowAction(decimal current, decimal target, decimal delta)
    {
        if (Math.Abs(current) <= 0 && Math.Abs(target) <= 0)
        {
            return "SKIP";
        }

        if (Math.Abs(current) > 0 && Math.Abs(target) <= 0)
        {
            return "CLOSE";
        }

        if (Math.Abs(current) <= 0)
        {
            return target > 0 ? "OPEN_LONG" : "OPEN_SHORT";
        }

        if (Math.Sign(current) != Math.Sign(target))
        {
            return "FLIP";
        }

        if (Math.Abs(delta) < ShadowMinRebalanceNotionalUsd)
        {
            return "HOLD";
        }

        return Math.Abs(target) > Math.Abs(current) ? "INCREASE" : "REDUCE";
    }

    private static string ShadowReason(decimal directionScore, decimal target, decimal delta, string action)
    {
        if (action == "SKIP" && Math.Abs(directionScore) < ShadowConsensusThreshold)
        {
            return "below_threshold";
        }

        if (action == "SKIP" && Math.Abs(target) < ShadowMinOrderNotionalUsd)
        {
            return "below_min_order";
        }

        if (action == "HOLD" && Math.Abs(delta) < ShadowMinRebalanceNotionalUsd)
        {
            return "delta_below_rebalance";
        }

        return string.Empty;
    }

    private async Task<List<object>> ApplyPlanRow(
        ShadowExecutionPlanRow row,
        IReadOnlyList<Core.Models.Position> positions,
        CancellationToken cancellationToken)
    {
        var results = new List<object>();
        var longPosition = positions.FirstOrDefault(x =>
            x.Symbol.Equals(row.Coin, StringComparison.OrdinalIgnoreCase) &&
            x.Direction.Equals("Long", StringComparison.OrdinalIgnoreCase));
        var shortPosition = positions.FirstOrDefault(x =>
            x.Symbol.Equals(row.Coin, StringComparison.OrdinalIgnoreCase) &&
            x.Direction.Equals("Short", StringComparison.OrdinalIgnoreCase));

        if (row.TargetSide == "LONG")
        {
            if (shortPosition is { MarginUsd: > 0 })
            {
                results.Add(await ExecuteSignal(row.Coin, Core.Models.TradeAction.CLOSE_SHORT, shortPosition.MarginUsd, "Close opposite short before consensus long"));
            }

            var currentMargin = longPosition?.MarginUsd ?? 0m;
            var deltaMargin = row.TargetMarginUsd - currentMargin;
            if (deltaMargin > 0)
            {
                results.Add(await ExecuteSignal(row.Coin, Core.Models.TradeAction.OPEN_LONG, deltaMargin, "Consensus target long delta"));
            }
            else if (deltaMargin < 0)
            {
                results.Add(await ExecuteSignal(row.Coin, Core.Models.TradeAction.CLOSE_LONG, Math.Abs(deltaMargin), "Consensus reduce long delta"));
            }
        }
        else if (row.TargetSide == "SHORT")
        {
            if (longPosition is { MarginUsd: > 0 })
            {
                results.Add(await ExecuteSignal(row.Coin, Core.Models.TradeAction.CLOSE_LONG, longPosition.MarginUsd, "Close opposite long before consensus short"));
            }

            var currentMargin = shortPosition?.MarginUsd ?? 0m;
            var deltaMargin = row.TargetMarginUsd - currentMargin;
            if (deltaMargin > 0)
            {
                results.Add(await ExecuteSignal(row.Coin, Core.Models.TradeAction.OPEN_SHORT, deltaMargin, "Consensus target short delta"));
            }
            else if (deltaMargin < 0)
            {
                results.Add(await ExecuteSignal(row.Coin, Core.Models.TradeAction.CLOSE_SHORT, Math.Abs(deltaMargin), "Consensus reduce short delta"));
            }
        }
        else
        {
            if (longPosition is { MarginUsd: > 0 })
            {
                results.Add(await ExecuteSignal(row.Coin, Core.Models.TradeAction.CLOSE_LONG, longPosition.MarginUsd, "Consensus flat long"));
            }

            if (shortPosition is { MarginUsd: > 0 })
            {
                results.Add(await ExecuteSignal(row.Coin, Core.Models.TradeAction.CLOSE_SHORT, shortPosition.MarginUsd, "Consensus flat short"));
            }
        }

        return results;

        async Task<object> ExecuteSignal(string coin, string action, decimal margin, string reason)
        {
            var trade = await _okxService.ExecuteTradeAsync(new Core.Models.TradeSignal
            {
                Decision = "TRADE",
                Symbol = coin,
                Action = action,
                Leverage = (int)ShadowLeverage,
                MarginAmountUSDT = margin,
                TradeConfidence = 100,
                SourceTxHash = $"hl-consensus:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Reason = reason
            });

            return new
            {
                success = trade.Success,
                action,
                coin,
                requestedMarginUsd = margin,
                trade.OrderId,
                trade.Size,
                trade.ErrorMessage
            };
        }
    }

    private static bool ResultSuccess(object value)
    {
        var property = value.GetType().GetProperty("success");
        return property?.GetValue(value) is true;
    }

    private static decimal EstimatedSignedOkxNotional(
        IReadOnlyList<Core.Models.Position> positions,
        string coin,
        decimal leverage)
    {
        return positions
            .Where(x => x.Symbol.Equals(coin, StringComparison.OrdinalIgnoreCase))
            .Sum(x =>
            {
                var notional = Math.Abs(x.MarginUsd) * leverage;
                return x.Direction.Equals("Short", StringComparison.OrdinalIgnoreCase)
                    ? -notional
                    : notional;
            });
    }

    private static decimal DesiredSignedNotional(string targetSide, decimal targetNotionalUsd) =>
        targetSide.Equals("SHORT", StringComparison.OrdinalIgnoreCase)
            ? -Math.Abs(targetNotionalUsd)
            : targetSide.Equals("LONG", StringComparison.OrdinalIgnoreCase)
                ? Math.Abs(targetNotionalUsd)
                : 0m;

    private static string NormalizeAddress(string value) => value.Trim().ToLowerInvariant();

    private sealed record ShadowTargetDraft(
        string Coin,
        decimal DirectionScore,
        decimal QualityScore,
        decimal ConflictRatio,
        decimal Participation,
        int ContributorCount,
        decimal SignedRawNotionalUsd,
        decimal SignedTargetNotionalUsd);

    private sealed class ShadowExecutionPlan
    {
        public string Mode { get; set; } = "Shadow Only";
        public ShadowExecutionPlanConfig Config { get; set; } = new();
        public ShadowExecutionPlanSummary Summary { get; set; } = new();
        public List<ShadowExecutionPlanRow> Rows { get; set; } = new();
    }

    private sealed class ShadowExecutionPlanConfig
    {
        public decimal Threshold { get; set; }
        public decimal Multiplier { get; set; }
        public decimal MinOrderNotionalUsd { get; set; }
        public decimal MinRebalanceNotionalUsd { get; set; }
        public decimal Leverage { get; set; }
        public string MarginMode { get; set; } = "isolated";
        public decimal MaxCoinMarginPct { get; set; }
        public decimal MaxTotalMarginPct { get; set; }
        public decimal EquityUsd { get; set; }
        public decimal MaxCoinNotionalUsd { get; set; }
        public decimal MaxTotalNotionalUsd { get; set; }
        public decimal TotalScaleApplied { get; set; }
    }

    private sealed class ShadowExecutionPlanSummary
    {
        public int ActiveTargetCoins { get; set; }
        public decimal GrossTargetNotionalUsd { get; set; }
        public decimal GrossTargetMarginUsd { get; set; }
        public int OpenActions { get; set; }
        public int CloseActions { get; set; }
        public int RebalanceActions { get; set; }
        public int HoldActions { get; set; }
    }

    private sealed class ShadowExecutionPlanRow
    {
        public string Coin { get; set; } = string.Empty;
        public decimal DirectionScore { get; set; }
        public decimal QualityScore { get; set; }
        public decimal ConflictRatio { get; set; }
        public decimal Participation { get; set; }
        public int ContributorCount { get; set; }
        public string TargetSide { get; set; } = "FLAT";
        public decimal RawTargetNotionalUsd { get; set; }
        public decimal SignedTargetNotionalUsd { get; set; }
        public decimal TargetNotionalUsd { get; set; }
        public decimal TargetMarginUsd { get; set; }
        public decimal CurrentOkxNotionalUsd { get; set; }
        public decimal DeltaNotionalUsd { get; set; }
        public string Action { get; set; } = string.Empty;
        public string SkipReason { get; set; } = string.Empty;
    }
}
