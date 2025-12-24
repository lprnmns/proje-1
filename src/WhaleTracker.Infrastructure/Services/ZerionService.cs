using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

/// <summary>
/// Zerion API Servisi
/// Balina cüzdanını takip eder
/// 
/// API Dokümantasyonu: https://developers.zerion.io/
/// </summary>
public class ZerionService : IZerionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ZerionService> _logger;
    private readonly ZerionSettings _settings;

    private const string BASE_URL = "https://api.zerion.io/v1";

    public ZerionService(
        HttpClient httpClient,
        ILogger<ZerionService> logger,
        IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value.Zerion;

        // API Key header'ı ayarla
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {_settings.ApiKey}");
    }

    /// <summary>
    /// Balinanın güncel portföyünü çeker
    /// </summary>
    public async Task<WhaleStats> GetWalletPortfolioAsync(string walletAddress)
    {
        // ================================================================
        // TODO: ZERION API'DEN PORTFÖY ÇEK
        // ================================================================
        // 
        // Endpoint: GET /wallets/{address}/portfolio
        // 
        // Örnek kod yapısı:
        // 
        // var url = $"{BASE_URL}/wallets/{walletAddress}/portfolio";
        // var response = await _httpClient.GetAsync(url);
        // 
        // if (!response.IsSuccessStatusCode)
        // {
        //     _logger.LogError("Zerion API hatası: {Status}", response.StatusCode);
        //     throw new Exception("Zerion API hatası");
        // }
        // 
        // var json = await response.Content.ReadAsStringAsync();
        // 
        // // JSON'u parse et ve WhaleStats'a dönüştür
        // var whaleStats = new WhaleStats
        // {
        //     TotalUsd = ...,
        //     Holdings = ...
        // };
        // 
        // return whaleStats;
        // ================================================================

        _logger.LogInformation("GetWalletPortfolioAsync çağrıldı: {Address}", walletAddress);

        throw new NotImplementedException("GetWalletPortfolioAsync metodunu implement et!");
    }

    /// <summary>
    /// Balinanın son işlemlerini çeker
    /// </summary>
    public async Task<List<TransactionEvent>> GetRecentTransactionsAsync(string walletAddress, int limit = 20)
    {
        // ================================================================
        // TODO: ZERION API'DEN SON İŞLEMLERİ ÇEK
        // ================================================================
        // 
        // Endpoint: GET /wallets/{address}/transactions
        // 
        // Bu metod çok önemli! Balina ne yaptı onu anlayacağız.
        // 
        // 1. API'den raw veriyi çek
        // 2. Her işlem için:
        //    - Direction: "Incoming" (alış) veya "Outgoing" (satış) belirle
        //    - TokenSymbol: WETH, USDC vs.
        //    - NormalizedSymbol: WETH -> ETH, WBTC -> BTC dönüşümü
        //    - Amount ve UsdValue hesapla
        // 
        // Örnek dönüşüm mantığı:
        // if (tokenSymbol.StartsWith("W")) 
        //     normalizedSymbol = tokenSymbol.Substring(1); // WETH -> ETH
        // ================================================================

        _logger.LogInformation("GetRecentTransactionsAsync çağrıldı: {Address}, Limit: {Limit}", walletAddress, limit);

        throw new NotImplementedException("GetRecentTransactionsAsync metodunu implement et!");
    }

    /// <summary>
    /// Belirli bir işlemin detayını çeker
    /// </summary>
    public async Task<TransactionEvent?> GetTransactionDetailAsync(string txHash)
    {
        // ================================================================
        // TODO: TEK BİR İŞLEMİN DETAYINI ÇEK
        // ================================================================
        // 
        // Endpoint: GET /transactions/{hash}
        // ================================================================

        _logger.LogInformation("GetTransactionDetailAsync çağrıldı: {TxHash}", txHash);

        throw new NotImplementedException("GetTransactionDetailAsync metodunu implement et!");
    }

    // ================================================================
    // YARDIMCI METODLAR (İstersen kullan)
    // ================================================================

    /// <summary>
    /// Token sembolünü normalize et (WETH -> ETH)
    /// </summary>
    private string NormalizeSymbol(string symbol)
    {
        // Wrapped tokenları düzelt
        var wrappedTokens = new Dictionary<string, string>
        {
            { "WETH", "ETH" },
            { "WBTC", "BTC" },
            { "WMATIC", "MATIC" },
            { "WAVAX", "AVAX" }
        };

        return wrappedTokens.TryGetValue(symbol.ToUpper(), out var normalized) 
            ? normalized 
            : symbol.ToUpper();
    }

    /// <summary>
    /// OKX'te işlem yapılabilir mi kontrol et
    /// </summary>
    private bool IsTradableOnOkx(string symbol)
    {
        var supportedCoins = new HashSet<string>
        {
            "ETH", "BTC", "SOL", "DOGE", "XRP", "ADA", "AVAX", "MATIC", 
            "LINK", "UNI", "AAVE", "LTC", "BCH", "DOT", "ATOM", "NEAR",
            "APT", "ARB", "OP", "SUI", "SEI", "TIA", "INJ", "FTM"
            // TODO: Daha fazla coin ekle
        };

        return supportedCoins.Contains(symbol.ToUpper());
    }
}
