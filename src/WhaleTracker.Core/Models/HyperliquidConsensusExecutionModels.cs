namespace WhaleTracker.Core.Models;

public sealed class HyperliquidConsensusExecutionPlan
{
    public string Mode { get; set; } = "Shadow Only";
    public HyperliquidConsensusExecutionConfig Config { get; set; } = new();
    public HyperliquidConsensusExecutionSummary Summary { get; set; } = new();
    public List<HyperliquidConsensusExecutionPlanRow> Rows { get; set; } = new();
}

public sealed class HyperliquidConsensusExecutionConfig
{
    public bool WorkerEnabled { get; set; }
    public decimal Threshold { get; set; }
    public decimal Multiplier { get; set; }
    public decimal MinOrderNotionalUsd { get; set; }
    public decimal MinRebalanceNotionalUsd { get; set; }
    public decimal Leverage { get; set; }
    public string MarginMode { get; set; } = "isolated";
    public string CoinWeightMode { get; set; } = "major_boost";
    public decimal MaxCoinMarginPct { get; set; }
    public decimal MaxTotalMarginPct { get; set; }
    public decimal EquityUsd { get; set; }
    public decimal MaxCoinNotionalUsd { get; set; }
    public decimal MaxTotalNotionalUsd { get; set; }
    public decimal TotalScaleApplied { get; set; }
}

public sealed class HyperliquidConsensusExecutionSummary
{
    public int ActiveTargetCoins { get; set; }
    public decimal GrossTargetNotionalUsd { get; set; }
    public decimal GrossTargetMarginUsd { get; set; }
    public int OpenActions { get; set; }
    public int CloseActions { get; set; }
    public int RebalanceActions { get; set; }
    public int HoldActions { get; set; }
}

public sealed class HyperliquidConsensusExecutionPlanRow
{
    public string Coin { get; set; } = string.Empty;
    public decimal DirectionScore { get; set; }
    public decimal QualityScore { get; set; }
    public decimal ConflictRatio { get; set; }
    public decimal Participation { get; set; }
    public int ContributorCount { get; set; }
    public decimal CoinWeight { get; set; } = 1m;
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

public sealed class HyperliquidConsensusApplyResult
{
    public bool Success { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Warning { get; set; } = string.Empty;
    public HyperliquidConsensusExecutionConfig Config { get; set; } = new();
    public HyperliquidConsensusExecutionSummary Summary { get; set; } = new();
    public List<HyperliquidConsensusAppliedRow> Results { get; set; } = new();
    public IReadOnlyList<Position> OpenOkxPositions { get; set; } = Array.Empty<Position>();
}

public sealed class HyperliquidConsensusAppliedRow
{
    public string Coin { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetSide { get; set; } = string.Empty;
    public decimal TargetMarginUsd { get; set; }
    public decimal SignedTargetNotionalUsd { get; set; }
    public List<HyperliquidConsensusOrderResult> Results { get; set; } = new();
}

public sealed class HyperliquidConsensusOrderResult
{
    public bool Success { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Coin { get; set; } = string.Empty;
    public decimal RequestedMarginUsd { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public decimal Size { get; set; }
    public string? ErrorMessage { get; set; }
}
