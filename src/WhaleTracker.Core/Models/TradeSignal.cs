namespace WhaleTracker.Core.Models;

/// <summary>
/// AI'ın ürettiği işlem sinyali
/// DecisionEngine'den çıkar, OkxService'e gider
/// </summary>
public class TradeSignal
{
    /// <summary>
    /// Karar: "TRADE" veya "IGNORE"
    /// </summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>
    /// Kararın sebebi (AI açıklaması)
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// İşlem yapılacak coin (ETH, BTC vs.)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Aksiyon: OPEN_LONG, OPEN_SHORT, CLOSE_LONG, CLOSE_SHORT
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Kaldıraç oranı
    /// </summary>
    public int Leverage { get; set; } = 2;

    /// <summary>
    /// İşlem büyüklüğü (USDT cinsinden marjin)
    /// </summary>
    public decimal MarginAmountUSDT { get; set; }

    /// <summary>
    /// AI'ın güven skoru (0-100)
    /// </summary>
    public int TradeConfidence { get; set; }

    /// <summary>
    /// Hangi balina işleminden türetildi
    /// </summary>
    public string SourceTxHash { get; set; } = string.Empty;

    /// <summary>
    /// Sinyal oluşturulma zamanı
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// İşlem aksiyonları için sabitler
/// </summary>
public static class TradeAction
{
    public const string OPEN_LONG = "OPEN_LONG";
    public const string OPEN_SHORT = "OPEN_SHORT";
    public const string CLOSE_LONG = "CLOSE_LONG";
    public const string CLOSE_SHORT = "CLOSE_SHORT";
    public const string IGNORE = "IGNORE";
}
