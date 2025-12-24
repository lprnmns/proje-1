namespace WhaleTracker.Core.Models;

/// <summary>
/// Balina cüzdanının anlık portföy durumu
/// Zerion API'den çekilir
/// </summary>
public class WhaleStats
{
    /// <summary>
    /// Balinanın toplam portföy değeri (USD)
    /// </summary>
    public decimal TotalUsd { get; set; }

    /// <summary>
    /// Balinanın elindeki tüm coinler
    /// </summary>
    public List<Holding> Holdings { get; set; } = new();
}

/// <summary>
/// Tek bir coin holding bilgisi
/// </summary>
public class Holding
{
    /// <summary>
    /// Coin sembolü (ETH, BTC, USDT vs.)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Miktar (örn: 50.5 ETH)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// USD değeri
    /// </summary>
    public decimal UsdValue { get; set; }
}
