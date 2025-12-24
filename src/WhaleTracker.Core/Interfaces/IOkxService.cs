using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

/// <summary>
/// OKX Futures API ile iletişim
/// İşlem açma/kapatma ve hesap bilgisi
/// </summary>
public interface IOkxService
{
    // ================================================================
    // HESAP BİLGİLERİ
    // ================================================================

    /// <summary>
    /// Kullanıcının hesap durumunu çeker
    /// </summary>
    Task<UserStats> GetAccountInfoAsync();

    /// <summary>
    /// Belirli bir coin için açık pozisyonu çeker
    /// </summary>
    Task<Position?> GetPositionAsync(string symbol);

    /// <summary>
    /// Tüm açık pozisyonları çeker
    /// </summary>
    Task<List<Position>> GetAllPositionsAsync();

    // ================================================================
    // 🏗️ DEMİR GİBİ MİMARİ - INSTRUMENT & ORDER HESAPLAMA
    // ================================================================

    /// <summary>
    /// Instrument bilgisini al (cache'li)
    /// Her coin için ctVal, minSz, lotSz değerlerini döner
    /// </summary>
    Task<InstrumentInfo?> GetInstrumentInfoAsync(string symbol, bool forceRefresh = false);

    /// <summary>
    /// 🎯 ANA HESAPLAMA METODU
    /// AI'dan gelen sinyali işlemeden önce tüm hesaplamaları yapar
    /// 
    /// Dönen OrderCalculation ile:
    /// - İşlem yapılabilir mi kontrol edilir
    /// - Gerçek margin/coin miktarı gösterilir
    /// - Uyarılar listelenir
    /// </summary>
    Task<OrderCalculation> CalculateOrderAsync(string symbol, decimal requestedMarginUSDT, int leverage, string action);

    // ================================================================
    // İŞLEM METODLARI
    // ================================================================

    /// <summary>
    /// İşlem sinyalini çalıştırır
    /// ANA METOD - Pseudo-code mantığı burada uygulanacak
    /// </summary>
    Task<TradeResult> ExecuteTradeAsync(TradeSignal signal);

    /// <summary>
    /// Market emri gönderir
    /// </summary>
    Task<TradeResult> PlaceMarketOrderAsync(string symbol, string side, string posSide, decimal size, bool reduceOnly = false);

    /// <summary>
    /// Pozisyonu tamamen kapatır
    /// </summary>
    Task<TradeResult> ClosePositionAsync(string symbol, string direction);

    /// <summary>
    /// Kaldıraç ayarlar
    /// </summary>
    Task<bool> SetLeverageAsync(string symbol, int leverage);

    // ================================================================
    // DEBUG / UYUMLULUK
    // ================================================================

    /// <summary>
    /// USDT miktarını kontrat sayısına çevirir (debug için)
    /// </summary>
    Task<(decimal contracts, decimal ctVal, decimal price, decimal notional, decimal minSz, decimal lotSz)> ConvertToContractsDebugAsync(string symbol, decimal usdtAmount, int leverage = 1);
}

