using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhaleTracker.Data.Entities;

/// <summary>
/// Veritabanına kaydedilen işlem logu
/// </summary>
[Table("trade_logs")]
public class TradeLogEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// Balina işlem hash'i
    /// </summary>
    [Column("whale_tx_hash")]
    [MaxLength(100)]
    public string WhaleTxHash { get; set; } = string.Empty;

    /// <summary>
    /// OKX order ID
    /// </summary>
    [Column("okx_order_id")]
    [MaxLength(50)]
    public string? OkxOrderId { get; set; }

    /// <summary>
    /// İşlem sembolü
    /// </summary>
    [Column("symbol")]
    [MaxLength(20)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Aksiyon (OPEN_LONG, CLOSE_SHORT vs.)
    /// </summary>
    [Column("action")]
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Kaldıraç
    /// </summary>
    [Column("leverage")]
    public int Leverage { get; set; }

    /// <summary>
    /// Marjin miktarı (USDT)
    /// </summary>
    [Column("margin_usdt")]
    public decimal MarginUsdt { get; set; }

    /// <summary>
    /// Gerçekleşen fiyat
    /// </summary>
    [Column("executed_price")]
    public decimal? ExecutedPrice { get; set; }

    /// <summary>
    /// İşlem başarılı mı?
    /// </summary>
    [Column("is_success")]
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Hata mesajı
    /// </summary>
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// AI'ın güven skoru
    /// </summary>
    [Column("confidence")]
    public int Confidence { get; set; }

    /// <summary>
    /// AI'ın açıklaması
    /// </summary>
    [Column("ai_reason")]
    public string? AiReason { get; set; }

    /// <summary>
    /// Oluşturulma zamanı
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
