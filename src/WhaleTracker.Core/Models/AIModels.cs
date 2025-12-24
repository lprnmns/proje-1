namespace WhaleTracker.Core.Models;

/// <summary>
/// AI'dan gelen işlem kararı
/// </summary>
public class AIDecision
{
    /// <summary>
    /// İşlem yapılacak mı?
    /// </summary>
    public bool ShouldTrade { get; set; }

    /// <summary>
    /// İşlem tipi: LONG, SHORT, IGNORE
    /// </summary>
    public string Action { get; set; } = "IGNORE";

    /// <summary>
    /// İşlem yapılacak coin sembolü
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// İşlem miktarı (USDT)
    /// </summary>
    public decimal AmountUSDT { get; set; }

    /// <summary>
    /// Kaldıraç
    /// </summary>
    public int Leverage { get; set; } = 3;

    /// <summary>
    /// AI'ın verdiği sebep
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Güven skoru (0-100)
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Ham AI yanıtı (debug için)
    /// </summary>
    public string RawResponse { get; set; } = string.Empty;

    /// <summary>
    /// Parse başarılı mı?
    /// </summary>
    public bool ParseSuccess { get; set; }

    /// <summary>
    /// Parse hatası varsa
    /// </summary>
    public string? ParseError { get; set; }
}

/// <summary>
/// AI'a gönderilecek context bilgisi
/// </summary>
public class AIContext
{
    /// <summary>
    /// Bizim (OKX) hesap bakiyesi
    /// </summary>
    public decimal OurBalanceUSDT { get; set; }

    /// <summary>
    /// Balina cüzdan bakiyesi
    /// </summary>
    public decimal WhaleBalanceUSDT { get; set; }

    /// <summary>
    /// Bizim açık pozisyonlarımız
    /// </summary>
    public List<OurPosition> OurPositions { get; set; } = new();

    /// <summary>
    /// Balinanın yeni hareketi
    /// </summary>
    public WhaleMovement NewMovement { get; set; } = new();
}

/// <summary>
/// Pozisyon özeti (AI için basitleştirilmiş)
/// </summary>
public class PositionSummary
{
    public string Symbol { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // "Long" veya "Short"
    public decimal MarginUSDT { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public int Leverage { get; set; }
    public decimal EntryPrice { get; set; }
}

/// <summary>
/// OurPosition - PositionSummary için alias
/// </summary>
public class OurPosition : PositionSummary { }

/// <summary>
/// Balina hareketi
/// </summary>
public class WhaleMovement
{
    /// <summary>
    /// İşlem tipi: BUY, SELL, TRANSFER_IN, TRANSFER_OUT
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// İşlem yapılan token sembolü
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Token miktarı
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// USD değeri
    /// </summary>
    public decimal ValueUSDT { get; set; }

    /// <summary>
    /// Token fiyatı
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// İşlem hash'i
    /// </summary>
    public string TxHash { get; set; } = string.Empty;

    /// <summary>
    /// İşlem zamanı
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Balinanın bu tokendaki toplam pozisyonu (işlem sonrası)
    /// </summary>
    public decimal WhalePositionAfter { get; set; }
}

/// <summary>
/// İşlem eşleştirme sonucu (SHORT için pozisyon arama)
/// </summary>
public class PositionMatchResult
{
    /// <summary>
    /// Eşleşme bulundu mu?
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// Eşleşen pozisyon
    /// </summary>
    public Position? MatchedPosition { get; set; }

    /// <summary>
    /// Kapatılacak miktar
    /// </summary>
    public decimal CloseAmount { get; set; }

    /// <summary>
    /// Tam kapatma mı?
    /// </summary>
    public bool FullClose { get; set; }

    /// <summary>
    /// Hata mesajı (eşleşme yoksa)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Tüm aday pozisyonlar (debug için)
    /// </summary>
    public List<Position> CandidatePositions { get; set; } = new();
}
