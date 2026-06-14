using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhaleTracker.Data.Entities;

[Table("hyperliquid_live_fills")]
public class HyperliquidLiveFillEntity
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
    [MaxLength(20)]
    public string Side { get; set; } = string.Empty;

    [Column("direction")]
    [MaxLength(60)]
    public string Direction { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }

    [Column("size")]
    public decimal Size { get; set; }

    [Column("closed_pnl_usd")]
    public decimal ClosedPnlUsd { get; set; }

    [Column("fee_usd")]
    public decimal FeeUsd { get; set; }

    [Column("exchange_time_ms")]
    public long ExchangeTimeMs { get; set; }

    [Column("exchange_time")]
    public DateTime ExchangeTime { get; set; }

    [Column("dedupe_key")]
    [MaxLength(220)]
    public string DedupeKey { get; set; } = string.Empty;

    [Column("raw_payload")]
    public string RawPayload { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
