namespace WhaleTracker.Core.Models;

/// <summary>
/// OKX API'den dönen işlem sonucu
/// </summary>
public class TradeResult
{
    /// <summary>
    /// İşlem başarılı mı?
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// OKX Order ID
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Coin sembolü (ETH, BTC vs.)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// İşlem yönü (buy/sell)
    /// </summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>
    /// Kontrat sayısı
    /// </summary>
    public decimal Size { get; set; }

    /// <summary>
    /// Hata mesajı (varsa)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gerçekleşen fiyat
    /// </summary>
    public decimal? ExecutedPrice { get; set; }

    /// <summary>
    /// Gerçekleşen miktar
    /// </summary>
    public decimal? ExecutedSize { get; set; }

    /// <summary>
    /// İşlem zamanı
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
