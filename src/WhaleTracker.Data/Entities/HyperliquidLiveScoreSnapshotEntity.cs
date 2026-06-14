using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhaleTracker.Data.Entities;

[Table("hyperliquid_live_score_snapshots")]
public class HyperliquidLiveScoreSnapshotEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("trader_address")]
    [MaxLength(100)]
    public string TraderAddress { get; set; } = string.Empty;

    [Column("scored_at")]
    public DateTime ScoredAt { get; set; } = DateTime.UtcNow;

    [Column("live_score")]
    public decimal LiveScore { get; set; }

    [Column("confidence")]
    public decimal Confidence { get; set; }

    [Column("realized_pnl_usd")]
    public decimal RealizedPnlUsd { get; set; }

    [Column("unrealized_pnl_usd")]
    public decimal UnrealizedPnlUsd { get; set; }

    [Column("net_pnl_usd")]
    public decimal NetPnlUsd { get; set; }

    [Column("pnl_pct_account")]
    public decimal PnlPctAccount { get; set; }

    [Column("closed_positions")]
    public int ClosedPositions { get; set; }

    [Column("active_positions")]
    public int ActivePositions { get; set; }

    [Column("wins")]
    public int Wins { get; set; }

    [Column("losses")]
    public int Losses { get; set; }

    [Column("win_rate")]
    public decimal WinRate { get; set; }

    [Column("okx_copyable_positions")]
    public int OkxCopyablePositions { get; set; }

    [Column("copied_positions")]
    public int CopiedPositions { get; set; }

    [Column("skipped_positions")]
    public int SkippedPositions { get; set; }

    [Column("avg_hold_seconds")]
    public decimal AvgHoldSeconds { get; set; }

    [Column("best_trade_usd")]
    public decimal BestTradeUsd { get; set; }

    [Column("worst_trade_usd")]
    public decimal WorstTradeUsd { get; set; }

    [Column("score_components_json")]
    public string ScoreComponentsJson { get; set; } = string.Empty;
}
