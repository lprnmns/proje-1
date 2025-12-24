using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhaleTracker.Data.Entities;

/// <summary>
/// İşlenen balina işlemleri (tekrar işlememek için)
/// </summary>
[Table("processed_transactions")]
public class ProcessedTransactionEntity
{
    [Key]
    [Column("tx_hash")]
    [MaxLength(100)]
    public string TxHash { get; set; } = string.Empty;

    [Column("processed_at")]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    [Column("was_traded")]
    public bool WasTraded { get; set; }
}
