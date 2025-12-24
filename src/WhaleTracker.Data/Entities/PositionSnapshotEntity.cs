using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhaleTracker.Data.Entities;

/// <summary>
/// Pozisyon anlık görüntüsü (geçmiş için)
/// </summary>
[Table("position_snapshots")]
public class PositionSnapshotEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("symbol")]
    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    [Column("direction")]
    [MaxLength(10)]
    public string Direction { get; set; } = string.Empty;

    [Column("margin_usd")]
    public decimal MarginUsd { get; set; }

    [Column("entry_price")]
    public decimal EntryPrice { get; set; }

    [Column("mark_price")]
    public decimal MarkPrice { get; set; }

    [Column("unrealized_pnl")]
    public decimal UnrealizedPnl { get; set; }

    [Column("snapshot_time")]
    public DateTime SnapshotTime { get; set; } = DateTime.UtcNow;
}
