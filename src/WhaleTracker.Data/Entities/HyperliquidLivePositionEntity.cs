using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhaleTracker.Data.Entities;

[Table("hyperliquid_live_positions")]
public class HyperliquidLivePositionEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("trader_address")]
    [MaxLength(100)]
    public string TraderAddress { get; set; } = string.Empty;

    [Column("coin")]
    [MaxLength(30)]
    public string Coin { get; set; } = string.Empty;

    [Column("okx_symbol")]
    [MaxLength(30)]
    public string OkxSymbol { get; set; } = string.Empty;

    [Column("side")]
    [MaxLength(10)]
    public string Side { get; set; } = string.Empty;

    [Column("status")]
    [MaxLength(40)]
    public string Status { get; set; } = string.Empty;

    [Column("opened_at")]
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    [Column("first_seen_at")]
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("entry_price")]
    public decimal EntryPrice { get; set; }

    [Column("exit_price")]
    public decimal ExitPrice { get; set; }

    [Column("current_size")]
    public decimal CurrentSize { get; set; }

    [Column("max_size")]
    public decimal MaxSize { get; set; }

    [Column("current_notional_usd")]
    public decimal CurrentNotionalUsd { get; set; }

    [Column("max_notional_usd")]
    public decimal MaxNotionalUsd { get; set; }

    [Column("source_account_value_at_open")]
    public decimal SourceAccountValueAtOpen { get; set; }

    [Column("latest_source_account_value_usd")]
    public decimal LatestSourceAccountValueUsd { get; set; }

    [Column("position_pct_of_account")]
    public decimal PositionPctOfAccount { get; set; }

    [Column("unrealized_pnl_usd")]
    public decimal UnrealizedPnlUsd { get; set; }

    [Column("realized_pnl_usd")]
    public decimal RealizedPnlUsd { get; set; }

    [Column("fee_usd")]
    public decimal FeeUsd { get; set; }

    [Column("net_pnl_usd")]
    public decimal NetPnlUsd { get; set; }

    [Column("opened_from_tracking")]
    public bool OpenedFromTracking { get; set; }

    [Column("is_okx_tradable")]
    public bool IsOkxTradable { get; set; }

    [Column("copy_status")]
    [MaxLength(60)]
    public string CopyStatus { get; set; } = string.Empty;

    [Column("skip_reason")]
    public string SkipReason { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
