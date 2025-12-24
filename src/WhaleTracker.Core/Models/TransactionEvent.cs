namespace WhaleTracker.Core.Models;

/// <summary>
/// Balinanın yaptığı bir işlem
/// Zerion Webhook veya Polling ile alınır
/// </summary>
public class TransactionEvent
{
    /// <summary>
    /// Benzersiz işlem ID
    /// </summary>
    public string TxHash { get; set; } = string.Empty;

    /// <summary>
    /// Hangi zincirde (ethereum, arbitrum, base vs.)
    /// </summary>
    public string Chain { get; set; } = string.Empty;

    /// <summary>
    /// İşlem yönü: "Incoming" (alış) veya "Outgoing" (satış)
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Token sembolü (WETH, USDC vs.)
    /// </summary>
    public string TokenSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Normalize edilmiş sembol (WETH -> ETH, WBTC -> BTC)
    /// </summary>
    public string NormalizedSymbol { get; set; } = string.Empty;

    /// <summary>
    /// İşlem miktarı
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// USD değeri
    /// </summary>
    public decimal UsdValue { get; set; }

    /// <summary>
    /// İşlem zamanı
    /// </summary>
    public DateTime BlockTimestamp { get; set; }

    /// <summary>
    /// İşlem tipi (transfer, swap, vs.)
    /// </summary>
    public string TransactionType { get; set; } = string.Empty;
}
