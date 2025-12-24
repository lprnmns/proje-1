namespace WhaleTracker.Core.Models;

/// <summary>
/// Kullanıcının OKX hesap durumu
/// </summary>
public class UserStats
{
    /// <summary>
    /// Kullanıcının toplam bakiyesi (USDT)
    /// </summary>
    public decimal TotalUsd { get; set; }

    /// <summary>
    /// Varsayılan kaldıraç ayarı
    /// </summary>
    public int Leverage { get; set; } = 2;

    /// <summary>
    /// Kullanıcının açık pozisyonları
    /// </summary>
    public List<Position> ActivePositions { get; set; } = new();
}

/// <summary>
/// Tek bir açık pozisyon
/// </summary>
public class Position
{
    /// <summary>
    /// Coin sembolü (ETH, BTC vs.)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Pozisyon yönü: "Long" veya "Short"
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Marjin miktarı (USDT)
    /// </summary>
    public decimal MarginUsd { get; set; }

    /// <summary>
    /// Giriş fiyatı
    /// </summary>
    public decimal EntryPrice { get; set; }

    /// <summary>
    /// Pozisyon boyutu (kontrat sayısı)
    /// </summary>
    public decimal Size { get; set; }

    /// <summary>
    /// Gerçekleşmemiş kar/zarar
    /// </summary>
    public decimal UnrealizedPnl { get; set; }
}
