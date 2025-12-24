namespace WhaleTracker.Core.Models;

/// <summary>
/// OKX Instrument (Enstrüman) Bilgisi
/// Her coin için kontrat özellikleri
/// 
/// Örnek DOGE:
///   ctVal = 1000 (1 kontrat = 1000 DOGE)
///   minSz = 0.01 (minimum 0.01 kontrat = 10 DOGE)
///   lotSz = 0.01 (0.01'in katları: 10, 20, 30... DOGE)
/// </summary>
public class InstrumentInfo
{
    /// <summary>
    /// Instrument ID (örn: DOGE-USDT-SWAP)
    /// </summary>
    public string InstId { get; set; } = "";

    /// <summary>
    /// Coin sembolü (örn: DOGE)
    /// </summary>
    public string Symbol { get; set; } = "";

    /// <summary>
    /// Contract Value - 1 TAM kontratın coin miktarı
    /// Örn: DOGE için 1000 (1 kontrat = 1000 DOGE)
    /// </summary>
    public decimal CtVal { get; set; } = 1;

    /// <summary>
    /// Minimum Size - Minimum emir büyüklüğü (kontrat cinsinden)
    /// Örn: DOGE için 0.01 (minimum 0.01 kontrat = 10 DOGE)
    /// </summary>
    public decimal MinSz { get; set; } = 0.01m;

    /// <summary>
    /// Lot Size - Emir artış miktarı (kontrat cinsinden)
    /// Örn: DOGE için 0.01 (0.01, 0.02, 0.03... kontrat)
    /// </summary>
    public decimal LotSz { get; set; } = 0.01m;

    /// <summary>
    /// Tick Size - Fiyat adımı
    /// </summary>
    public decimal TickSz { get; set; } = 0.00001m;

    /// <summary>
    /// Maksimum kaldıraç
    /// </summary>
    public int MaxLeverage { get; set; } = 50;

    /// <summary>
    /// Güncel fiyat (cache'den)
    /// </summary>
    public decimal LastPrice { get; set; }

    /// <summary>
    /// Fiyat güncellenme zamanı
    /// </summary>
    public DateTime PriceUpdatedAt { get; set; }

    /// <summary>
    /// Instrument bilgisi güncellenme zamanı
    /// </summary>
    public DateTime InfoUpdatedAt { get; set; }

    // ================================================================
    // HESAPLAMA YARDIMCILARI
    // ================================================================

    /// <summary>
    /// 1 TAM kontratın USD değeri
    /// Örn: DOGE için 1000 * 0.13 = 130 USD
    /// </summary>
    public decimal OneFullContractUsd => CtVal * LastPrice;

    /// <summary>
    /// Minimum kontratın coin miktarı
    /// Örn: DOGE için 0.01 * 1000 = 10 DOGE
    /// </summary>
    public decimal MinCoinAmount => MinSz * CtVal;

    /// <summary>
    /// Lot artış miktarının coin karşılığı
    /// Örn: DOGE için 0.01 * 1000 = 10 DOGE
    /// </summary>
    public decimal LotCoinAmount => LotSz * CtVal;

    /// <summary>
    /// Minimum kontratın USD değeri
    /// Örn: DOGE için 10 * 0.13 = 1.30 USD
    /// </summary>
    public decimal MinContractUsd => MinSz * OneFullContractUsd;

    /// <summary>
    /// Belirli kaldıraç için minimum margin
    /// Örn: DOGE, 3x için: 1.30 / 3 = 0.43 USD
    /// </summary>
    public decimal GetMinMarginForLeverage(int leverage) => MinContractUsd / leverage;
}
