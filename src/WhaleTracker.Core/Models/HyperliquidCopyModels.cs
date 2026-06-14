namespace WhaleTracker.Core.Models;

public class HyperliquidCopyEnableRequest
{
    public IReadOnlyList<string> TraderAddresses { get; set; } = Array.Empty<string>();
    public decimal MarginPerTraderUsdt { get; set; } = 10m;
    public int Leverage { get; set; } = 10;
    public bool ExecuteOrders { get; set; }
    public bool CopyActiveOnEnable { get; set; } = true;
    public bool AdoptActiveOnlyWhenNegative { get; set; } = true;
    public bool SyncImmediately { get; set; } = true;
    public string LabelPrefix { get; set; } = "Hyperliquid";
}

public class HyperliquidCopySyncRequest
{
    public IReadOnlyList<string> TraderAddresses { get; set; } = Array.Empty<string>();
    public bool ExecuteOrders { get; set; }
    public bool OverrideExecuteOrders { get; set; }
}

public class HyperliquidCopyTraderView
{
    public string Address { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool ExecuteOrders { get; set; }
    public decimal MarginPerTraderUsdt { get; set; }
    public int Leverage { get; set; }
    public bool AdoptActiveOnlyWhenNegative { get; set; }
    public bool CopyActiveOnEnable { get; set; }
    public long LastSeenFillTimeMs { get; set; }
    public DateTime? LastFillPollAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string LastError { get; set; } = string.Empty;
}

public class HyperliquidCopyPositionView
{
    public string TraderAddress { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal SourceSize { get; set; }
    public decimal SourceEntryPrice { get; set; }
    public decimal SourcePositionValueUsd { get; set; }
    public decimal SourceMarginUsedUsd { get; set; }
    public decimal SourceUnrealizedPnlUsd { get; set; }
    public decimal SourceAccountValueUsd { get; set; }
    public decimal SourceExposurePercent { get; set; }
    public decimal SourceMarginPercent { get; set; }
    public decimal TargetMarginUsdt { get; set; }
    public decimal SizingBudgetUsdt { get; set; }
    public int SizingLeverage { get; set; }
    public int SizingVersion { get; set; }
    public DateTime LastSourceSeenAt { get; set; }
    public DateTime? LastCopiedAt { get; set; }
    public string LastMessage { get; set; } = string.Empty;
}

public class HyperliquidCopyEventView
{
    public long Id { get; set; }
    public string TraderAddress { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string SourceEventId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal TargetMarginUsdt { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class HyperliquidCopySnapshot
{
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public IReadOnlyList<HyperliquidCopyTraderView> Traders { get; set; } =
        Array.Empty<HyperliquidCopyTraderView>();
    public IReadOnlyList<HyperliquidCopyPositionView> Positions { get; set; } =
        Array.Empty<HyperliquidCopyPositionView>();
    public IReadOnlyList<HyperliquidCopyEventView> RecentEvents { get; set; } =
        Array.Empty<HyperliquidCopyEventView>();
}

public class HyperliquidLiveLeaderboardResponse
{
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public IReadOnlyList<HyperliquidLiveTraderScoreView> Traders { get; set; } =
        Array.Empty<HyperliquidLiveTraderScoreView>();
    public IReadOnlyList<HyperliquidLivePositionView> ActivePositions { get; set; } =
        Array.Empty<HyperliquidLivePositionView>();
    public IReadOnlyList<HyperliquidLivePositionView> ClosedPositions { get; set; } =
        Array.Empty<HyperliquidLivePositionView>();
    public IReadOnlyList<HyperliquidLiveFillView> RecentFills { get; set; } =
        Array.Empty<HyperliquidLiveFillView>();
}

public class HyperliquidLiveTraderScoreView
{
    public string TraderAddress { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool ExecuteOrders { get; set; }
    public decimal LiveScore { get; set; }
    public decimal Confidence { get; set; }
    public decimal RealizedPnlUsd { get; set; }
    public decimal UnrealizedPnlUsd { get; set; }
    public decimal NetPnlUsd { get; set; }
    public decimal PnlPctAccount { get; set; }
    public int ClosedPositions { get; set; }
    public int ActivePositions { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal WinRate { get; set; }
    public int OkxCopyablePositions { get; set; }
    public int CopiedPositions { get; set; }
    public int SkippedPositions { get; set; }
    public decimal AvgHoldSeconds { get; set; }
    public decimal BestTradeUsd { get; set; }
    public decimal WorstTradeUsd { get; set; }
    public DateTime ScoredAt { get; set; }
}

public class HyperliquidLivePositionView
{
    public long Id { get; set; }
    public string TraderAddress { get; set; } = string.Empty;
    public string Coin { get; set; } = string.Empty;
    public string OkxSymbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal CurrentSize { get; set; }
    public decimal MaxSize { get; set; }
    public decimal CurrentNotionalUsd { get; set; }
    public decimal MaxNotionalUsd { get; set; }
    public decimal PositionPctOfAccount { get; set; }
    public decimal UnrealizedPnlUsd { get; set; }
    public decimal RealizedPnlUsd { get; set; }
    public decimal FeeUsd { get; set; }
    public decimal NetPnlUsd { get; set; }
    public bool OpenedFromTracking { get; set; }
    public bool IsOkxTradable { get; set; }
    public string CopyStatus { get; set; } = string.Empty;
    public string SkipReason { get; set; } = string.Empty;
    public decimal DurationSeconds { get; set; }
    public decimal PnlPctAccount { get; set; }
    public decimal PnlPctNotional { get; set; }
}

public class HyperliquidLiveFillView
{
    public string TraderAddress { get; set; } = string.Empty;
    public string Coin { get; set; } = string.Empty;
    public string OkxSymbol { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Size { get; set; }
    public decimal ClosedPnlUsd { get; set; }
    public decimal FeeUsd { get; set; }
    public DateTime ExchangeTime { get; set; }
}

public class HyperliquidCopyEnableResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public HyperliquidCopySnapshot Snapshot { get; set; } = new();
    public IReadOnlyList<HyperliquidCopyTraderSyncResult> SyncResults { get; set; } =
        Array.Empty<HyperliquidCopyTraderSyncResult>();
}

public class HyperliquidCopyTraderSyncResult
{
    public string TraderAddress { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ActiveSourcePositions { get; set; }
    public int CopiedPositions { get; set; }
    public int SkippedPositions { get; set; }
    public int ClosedTargets { get; set; }
    public int NewFills { get; set; }
    public IReadOnlyList<HyperliquidCopyPositionDecision> Decisions { get; set; } =
        Array.Empty<HyperliquidCopyPositionDecision>();
}

public class HyperliquidCopyPositionDecision
{
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal SourceUnrealizedPnlUsd { get; set; }
    public decimal SourcePositionValueUsd { get; set; }
    public decimal SourceAccountValueUsd { get; set; }
    public decimal SourceExposurePercent { get; set; }
    public decimal SourceMarginPercent { get; set; }
    public decimal TargetMarginUsdt { get; set; }
    public CopyPositionTargetResult? OkxResult { get; set; }
}

public static class HyperliquidCopySizingMath
{
    public static decimal TargetMarginUsdt(
        decimal traderBudgetUsdt,
        decimal sourcePositionValueUsd,
        decimal sourceAccountValueUsd,
        int targetLeverage)
    {
        if (traderBudgetUsdt <= 0 ||
            sourceAccountValueUsd <= 0 ||
            targetLeverage <= 0)
        {
            return 0;
        }

        var normalizedExposure = Math.Abs(sourcePositionValueUsd) / sourceAccountValueUsd;
        return traderBudgetUsdt * normalizedExposure / targetLeverage;
    }

    public static decimal ExposurePercent(
        decimal sourcePositionValueUsd,
        decimal sourceAccountValueUsd)
    {
        return sourceAccountValueUsd > 0
            ? Math.Abs(sourcePositionValueUsd) / sourceAccountValueUsd * 100m
            : 0m;
    }

    public static decimal MarginPercent(
        decimal sourceMarginUsedUsd,
        decimal sourceAccountValueUsd)
    {
        return sourceAccountValueUsd > 0
            ? Math.Abs(sourceMarginUsedUsd) / sourceAccountValueUsd * 100m
            : 0m;
    }
}
