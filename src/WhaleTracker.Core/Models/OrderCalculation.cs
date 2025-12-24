namespace WhaleTracker.Core.Models;

/// <summary>
/// Emir Hesaplama Sonucu
/// AI'dan gelen sinyali işlemeden önce hesaplanan detaylı bilgi
/// 
/// Bu bilgi ile:
/// 1. Emrin geçerli olup olmadığı kontrol edilir
/// 2. Gerçek margin/coin miktarı hesaplanır
/// 3. İşlem öncesi onay alınabilir (opsiyonel)
/// </summary>
public class OrderCalculation
{
    // ================================================================
    // GİRDİ BİLGİLERİ (AI'dan gelen)
    // ================================================================

    /// <summary>
    /// Coin sembolü (ETH, BTC, DOGE vs.)
    /// </summary>
    public string Symbol { get; set; } = "";

    /// <summary>
    /// İstenen margin miktarı (USDT)
    /// </summary>
    public decimal RequestedMarginUSDT { get; set; }

    /// <summary>
    /// Kaldıraç
    /// </summary>
    public int Leverage { get; set; }

    /// <summary>
    /// İşlem yönü (OPEN_LONG, OPEN_SHORT, CLOSE_LONG, CLOSE_SHORT)
    /// </summary>
    public string Action { get; set; } = "";

    // ================================================================
    // ENSTRÜMAN BİLGİLERİ
    // ================================================================

    /// <summary>
    /// Enstrüman detayları
    /// </summary>
    public InstrumentInfo? Instrument { get; set; }

    // ================================================================
    // HESAPLAMA SONUÇLARI
    // ================================================================

    /// <summary>
    /// Hesaplanan kontrat sayısı (lotSz'ye yuvarlanmış)
    /// </summary>
    public decimal Contracts { get; set; }

    /// <summary>
    /// Alınacak/satılacak coin miktarı
    /// </summary>
    public decimal CoinAmount { get; set; }

    /// <summary>
    /// Gerçek pozisyon değeri (USD)
    /// </summary>
    public decimal PositionValueUSD { get; set; }

    /// <summary>
    /// Gerçek margin miktarı (USD)
    /// </summary>
    public decimal ActualMarginUSD { get; set; }

    /// <summary>
    /// İstenen ve gerçek margin arasındaki fark
    /// Pozitif = fazla margin, Negatif = az margin
    /// </summary>
    public decimal MarginDifference { get; set; }

    /// <summary>
    /// Margin sapma yüzdesi (%)
    /// </summary>
    public decimal MarginDeviationPercent { get; set; }

    // ================================================================
    // VALİDASYON SONUÇLARI
    // ================================================================

    /// <summary>
    /// İşlem yapılabilir mi?
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validasyon durumu
    /// </summary>
    public OrderValidationStatus ValidationStatus { get; set; }

    /// <summary>
    /// Validasyon mesajı (Türkçe)
    /// </summary>
    public string ValidationMessage { get; set; } = "";

    /// <summary>
    /// Uyarılar (işlem yapılabilir ama dikkat edilmeli)
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    // ================================================================
    // HESAPLAMA DETAYLARI (Debug/Log için)
    // ================================================================

    /// <summary>
    /// Ham kontrat sayısı (yuvarlamadan önce)
    /// </summary>
    public decimal RawContracts { get; set; }

    /// <summary>
    /// Notional değer (margin * leverage)
    /// </summary>
    public decimal Notional { get; set; }

    /// <summary>
    /// Hesaplama adımları (debug için)
    /// </summary>
    public List<string> CalculationSteps { get; set; } = new();
}

/// <summary>
/// Emir validasyon durumları
/// </summary>
public enum OrderValidationStatus
{
    /// <summary>
    /// ✅ Geçerli - İşlem yapılabilir
    /// </summary>
    Valid,

    /// <summary>
    /// ⚠️ Uyarı ile geçerli - İşlem yapılabilir ama margin farklı olacak
    /// </summary>
    ValidWithWarning,

    /// <summary>
    /// ❌ Geçersiz - Margin çok küçük, minimum kontrat bile açılamaz
    /// </summary>
    InsufficientMargin,

    /// <summary>
    /// ❌ Geçersiz - Kaldıraç çok yüksek
    /// </summary>
    LeverageTooHigh,

    /// <summary>
    /// ❌ Geçersiz - Enstrüman bulunamadı
    /// </summary>
    InstrumentNotFound,

    /// <summary>
    /// ❌ Geçersiz - Fiyat alınamadı
    /// </summary>
    PriceUnavailable,

    /// <summary>
    /// ❌ Geçersiz - Bilinmeyen hata
    /// </summary>
    Error
}
