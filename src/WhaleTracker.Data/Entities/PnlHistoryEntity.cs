using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhaleTracker.Data.Entities;

/// <summary>
/// PnL geçmişi (günlük/saatlik)
/// </summary>
[Table("pnl_history")]
public class PnlHistoryEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// Toplam bakiye o an
    /// </summary>
    [Column("total_balance")]
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Gerçekleşmiş PnL (o ana kadar)
    /// </summary>
    [Column("realized_pnl")]
    public decimal RealizedPnl { get; set; }

    /// <summary>
    /// Gerçekleşmemiş PnL
    /// </summary>
    [Column("unrealized_pnl")]
    public decimal UnrealizedPnl { get; set; }

    /// <summary>
    /// Kayıt zamanı
    /// </summary>
    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
