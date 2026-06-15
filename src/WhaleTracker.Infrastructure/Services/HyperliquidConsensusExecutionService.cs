using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;
using WhaleTracker.Data;

namespace WhaleTracker.Infrastructure.Services;

public sealed class HyperliquidConsensusExecutionService : IHyperliquidConsensusExecutionService
{
    private static readonly HashSet<string> MajorCoins = new(StringComparer.OrdinalIgnoreCase)
    {
        "BTC", "ETH", "SOL"
    };

    private static readonly HashSet<string> Tier2Coins = new(StringComparer.OrdinalIgnoreCase)
    {
        "BNB", "XRP", "DOGE", "ADA", "AVAX", "LINK", "SUI", "LTC", "BCH", "TRX", "TON", "HYPE"
    };

    private static readonly HashSet<string> HighWickCoins = new(StringComparer.OrdinalIgnoreCase)
    {
        "XPL", "GRASS", "FARTCOIN", "MEGA", "ZORA", "PUMP", "MOODENG", "POPCAT", "TRUMP"
    };

    private readonly WhaleTrackerDbContext _db;
    private readonly IHyperliquidConsensusService _consensusService;
    private readonly IOkxService _okxService;
    private readonly HyperliquidConsensusExecutionSettings _settings;
    private readonly ILogger<HyperliquidConsensusExecutionService> _logger;

    public HyperliquidConsensusExecutionService(
        WhaleTrackerDbContext db,
        IHyperliquidConsensusService consensusService,
        IOkxService okxService,
        IOptions<AppSettings> options,
        ILogger<HyperliquidConsensusExecutionService> logger)
    {
        _db = db;
        _consensusService = consensusService;
        _okxService = okxService;
        _settings = options.Value.HyperliquidConsensusExecution;
        _logger = logger;
    }

    public async Task<HyperliquidConsensusExecutionPlan> BuildPlanAsync(CancellationToken cancellationToken = default)
    {
        var okx = await SafeOkxAccount(cancellationToken);
        var consensus = await _consensusService.GetSnapshotAsync(cancellationToken);
        return BuildPlan(consensus.Coins, okx?.ActivePositions ?? new List<Position>(), okx?.TotalUsd ?? 100m);
    }

    public async Task<HyperliquidConsensusApplyResult> ApplyPlanAsync(CancellationToken cancellationToken = default)
    {
        await _db.HyperliquidCopyTraders.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.ExecuteOrders, false).SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken);

        var plan = await BuildPlanAsync(cancellationToken);
        var applied = new List<HyperliquidConsensusAppliedRow>();

        foreach (var row in plan.Rows.Where(x => x.Action is not ("SKIP" or "HOLD")))
        {
            var before = await _okxService.GetAllPositionsAsync();
            var rowResults = await ApplyPlanRow(row, before, cancellationToken);
            applied.Add(new HyperliquidConsensusAppliedRow
            {
                Coin = row.Coin,
                Action = row.Action,
                TargetSide = row.TargetSide,
                TargetMarginUsd = row.TargetMarginUsd,
                SignedTargetNotionalUsd = row.SignedTargetNotionalUsd,
                Results = rowResults
            });
        }

        var after = await _okxService.GetAllPositionsAsync();
        return new HyperliquidConsensusApplyResult
        {
            Success = applied.SelectMany(x => x.Results).All(x => x.Success),
            Mode = "Consensus real apply",
            Warning = "Legacy per-trader Hyperliquid ExecuteOrders flags were forced off before applying this consensus plan.",
            Config = plan.Config,
            Summary = plan.Summary,
            Results = applied,
            OpenOkxPositions = after
        };
    }

    private HyperliquidConsensusExecutionPlan BuildPlan(
        IReadOnlyList<CoinConsensusView> coins,
        IReadOnlyList<Position> okxPositions,
        decimal equityUsd)
    {
        var equity = equityUsd > 0 ? equityUsd : 100m;
        var leverage = Math.Max(_settings.Leverage, 1m);
        var maxCoinNotional = equity * leverage * _settings.MaxCoinMarginPct;
        var maxTotalNotional = equity * leverage * _settings.MaxTotalMarginPct;

        var rawRows = coins.Select(coin =>
        {
            var coinWeight = CoinWeight(coin.Coin);
            var signedRaw = Math.Abs(coin.DirectionScore) < _settings.Threshold
                ? 0m
                : coin.DirectionScore * _settings.Multiplier * coinWeight;
            var signedCapped = Math.Clamp(signedRaw, -maxCoinNotional, maxCoinNotional);
            if (Math.Abs(signedCapped) < _settings.MinOrderNotionalUsd)
            {
                signedCapped = 0m;
            }

            return new TargetDraft(
                coin.Coin,
                coin.DirectionScore,
                coin.QualityScore,
                coin.ConflictRatio,
                coin.Participation,
                coin.ContributorCount,
                coinWeight,
                signedRaw,
                signedCapped);
        }).ToList();

        var gross = rawRows.Sum(x => Math.Abs(x.SignedTargetNotionalUsd));
        var scale = gross > maxTotalNotional && maxTotalNotional > 0 ? maxTotalNotional / gross : 1m;
        var rows = rawRows.Select(row =>
            {
                var signedTarget = row.SignedTargetNotionalUsd * scale;
                if (Math.Abs(signedTarget) < _settings.MinOrderNotionalUsd)
                {
                    signedTarget = 0m;
                }

                var current = EstimatedSignedOkxNotional(okxPositions, row.Coin, leverage);
                var delta = signedTarget - current;
                var action = ActionFor(current, signedTarget, delta);
                var reason = ReasonFor(row.DirectionScore, signedTarget, delta, action);
                return new HyperliquidConsensusExecutionPlanRow
                {
                    Coin = row.Coin,
                    DirectionScore = row.DirectionScore,
                    QualityScore = row.QualityScore,
                    ConflictRatio = row.ConflictRatio,
                    Participation = row.Participation,
                    ContributorCount = row.ContributorCount,
                    CoinWeight = row.CoinWeight,
                    TargetSide = signedTarget > 0 ? "LONG" : signedTarget < 0 ? "SHORT" : "FLAT",
                    RawTargetNotionalUsd = row.SignedRawNotionalUsd,
                    SignedTargetNotionalUsd = signedTarget,
                    TargetNotionalUsd = Math.Abs(signedTarget),
                    TargetMarginUsd = Math.Abs(signedTarget) / leverage,
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
        return new HyperliquidConsensusExecutionPlan
        {
            Mode = _settings.Enabled ? "Consensus Real Enabled" : "Shadow Only",
            Config = new HyperliquidConsensusExecutionConfig
            {
                WorkerEnabled = _settings.Enabled,
                Threshold = _settings.Threshold,
                Multiplier = _settings.Multiplier,
                MinOrderNotionalUsd = _settings.MinOrderNotionalUsd,
                MinRebalanceNotionalUsd = _settings.MinRebalanceNotionalUsd,
                Leverage = leverage,
                MarginMode = _settings.MarginMode,
                CoinWeightMode = _settings.CoinWeightMode,
                MaxCoinMarginPct = _settings.MaxCoinMarginPct,
                MaxTotalMarginPct = _settings.MaxTotalMarginPct,
                EquityUsd = equity,
                MaxCoinNotionalUsd = maxCoinNotional,
                MaxTotalNotionalUsd = maxTotalNotional,
                TotalScaleApplied = scale
            },
            Summary = new HyperliquidConsensusExecutionSummary
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

    private decimal CoinWeight(string coin)
    {
        if (!_settings.CoinWeightMode.Equals("major_boost", StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        if (coin.Equals("BTC", StringComparison.OrdinalIgnoreCase))
        {
            return 1.75m;
        }

        if (MajorCoins.Contains(coin))
        {
            return 1.5m;
        }

        if (Tier2Coins.Contains(coin))
        {
            return 1m;
        }

        if (HighWickCoins.Contains(coin))
        {
            return 0.35m;
        }

        return 0.6m;
    }

    private string ActionFor(decimal current, decimal target, decimal delta)
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

        if (Math.Abs(delta) < _settings.MinRebalanceNotionalUsd)
        {
            return "HOLD";
        }

        return Math.Abs(target) > Math.Abs(current) ? "INCREASE" : "REDUCE";
    }

    private string ReasonFor(decimal directionScore, decimal target, decimal delta, string action)
    {
        if (action == "SKIP" && Math.Abs(directionScore) < _settings.Threshold)
        {
            return "below_threshold";
        }

        if (action == "SKIP" && Math.Abs(target) < _settings.MinOrderNotionalUsd)
        {
            return "below_min_order";
        }

        if (action == "HOLD" && Math.Abs(delta) < _settings.MinRebalanceNotionalUsd)
        {
            return "delta_below_rebalance";
        }

        return string.Empty;
    }

    private async Task<List<HyperliquidConsensusOrderResult>> ApplyPlanRow(
        HyperliquidConsensusExecutionPlanRow row,
        IReadOnlyList<Position> positions,
        CancellationToken cancellationToken)
    {
        var results = new List<HyperliquidConsensusOrderResult>();
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
                results.Add(await ExecuteSignal(row.Coin, TradeAction.CLOSE_SHORT, shortPosition.MarginUsd, "Close opposite short before consensus long"));
            }

            var currentMargin = longPosition?.MarginUsd ?? 0m;
            var deltaMargin = row.TargetMarginUsd - currentMargin;
            if (deltaMargin > 0)
            {
                results.Add(await ExecuteSignal(row.Coin, TradeAction.OPEN_LONG, deltaMargin, "Consensus target long delta"));
            }
            else if (deltaMargin < 0)
            {
                results.Add(await ExecuteSignal(row.Coin, TradeAction.CLOSE_LONG, Math.Abs(deltaMargin), "Consensus reduce long delta"));
            }
        }
        else if (row.TargetSide == "SHORT")
        {
            if (longPosition is { MarginUsd: > 0 })
            {
                results.Add(await ExecuteSignal(row.Coin, TradeAction.CLOSE_LONG, longPosition.MarginUsd, "Close opposite long before consensus short"));
            }

            var currentMargin = shortPosition?.MarginUsd ?? 0m;
            var deltaMargin = row.TargetMarginUsd - currentMargin;
            if (deltaMargin > 0)
            {
                results.Add(await ExecuteSignal(row.Coin, TradeAction.OPEN_SHORT, deltaMargin, "Consensus target short delta"));
            }
            else if (deltaMargin < 0)
            {
                results.Add(await ExecuteSignal(row.Coin, TradeAction.CLOSE_SHORT, Math.Abs(deltaMargin), "Consensus reduce short delta"));
            }
        }
        else
        {
            if (longPosition is { MarginUsd: > 0 })
            {
                results.Add(await ExecuteSignal(row.Coin, TradeAction.CLOSE_LONG, longPosition.MarginUsd, "Consensus flat long"));
            }

            if (shortPosition is { MarginUsd: > 0 })
            {
                results.Add(await ExecuteSignal(row.Coin, TradeAction.CLOSE_SHORT, shortPosition.MarginUsd, "Consensus flat short"));
            }
        }

        return results;

        async Task<HyperliquidConsensusOrderResult> ExecuteSignal(string coin, string action, decimal margin, string reason)
        {
            var trade = await _okxService.ExecuteTradeAsync(new TradeSignal
            {
                Decision = "TRADE",
                Symbol = coin,
                Action = action,
                Leverage = (int)Math.Max(_settings.Leverage, 1m),
                MarginAmountUSDT = margin,
                TradeConfidence = 100,
                SourceTxHash = $"hl-consensus:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                Reason = reason
            });

            if (!trade.Success)
            {
                _logger.LogWarning(
                    "Consensus OKX order rejected: {Coin} {Action} margin {Margin} reason {Error}",
                    coin,
                    action,
                    margin,
                    trade.ErrorMessage);
            }

            return new HyperliquidConsensusOrderResult
            {
                Success = trade.Success,
                Action = action,
                Coin = coin,
                RequestedMarginUsd = margin,
                OrderId = trade.OrderId,
                Size = trade.Size,
                ErrorMessage = trade.ErrorMessage
            };
        }
    }

    private async Task<UserStats?> SafeOkxAccount(CancellationToken cancellationToken)
    {
        try
        {
            return await _okxService.GetAccountInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OKX account read failed while building consensus plan.");
            return null;
        }
    }

    private static decimal EstimatedSignedOkxNotional(
        IReadOnlyList<Position> positions,
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

    private sealed record TargetDraft(
        string Coin,
        decimal DirectionScore,
        decimal QualityScore,
        decimal ConflictRatio,
        decimal Participation,
        int ContributorCount,
        decimal CoinWeight,
        decimal SignedRawNotionalUsd,
        decimal SignedTargetNotionalUsd);
}
