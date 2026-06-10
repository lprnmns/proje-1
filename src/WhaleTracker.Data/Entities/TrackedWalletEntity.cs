using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhaleTracker.Data.Entities;

[Table("tracked_wallets")]
public class TrackedWalletEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("wallet_address")]
    [MaxLength(100)]
    public string WalletAddress { get; set; } = string.Empty;

    [Column("label")]
    [MaxLength(120)]
    public string Label { get; set; } = string.Empty;

    [Column("source")]
    [MaxLength(60)]
    public string Source { get; set; } = "manual";

    [Column("chain")]
    [MaxLength(40)]
    public string Chain { get; set; } = "ethereum";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("confidence_score")]
    public decimal ConfidenceScore { get; set; }

    [Column("estimated_profit_usd")]
    public decimal EstimatedProfitUsd { get; set; }

    [Column("asset_symbol")]
    [MaxLength(20)]
    public string AssetSymbol { get; set; } = string.Empty;

    [Column("historical_scan_id")]
    public long? HistoricalScanId { get; set; }

    [Column("insider_candidate_id")]
    public long? InsiderCandidateId { get; set; }

    [Column("notes")]
    public string Notes { get; set; } = string.Empty;

    [Column("last_checked_at")]
    public DateTime? LastCheckedAt { get; set; }

    [Column("last_seen_tx_hash")]
    [MaxLength(100)]
    public string? LastSeenTxHash { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
