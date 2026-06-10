using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

/// <summary>
/// AI Karar Motoru
/// OpenAI API ile balina işlemlerini analiz eder
/// 
/// Senin verdiğin System Prompt ve User Prompt yapısı burada uygulanacak
/// </summary>
public class DecisionEngine : IDecisionEngine
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DecisionEngine> _logger;
    private readonly OpenAiSettings _settings;

    private const string OPENAI_URL = "https://api.openai.com/v1/chat/completions";

    // OKX'te desteklenen coin listesi
    private static readonly HashSet<string> SupportedCoins = new()
    {
        "ETH", "BTC", "SOL", "DOGE", "XRP", "ADA", "AVAX", "MATIC",
        "LINK", "UNI", "AAVE", "LTC", "BCH", "DOT", "ATOM", "NEAR",
        "APT", "ARB", "OP", "SUI", "SEI", "TIA", "INJ", "FTM",
        "PEPE", "SHIB", "WIF", "BONK", "ORDI", "SATS"
    };

    public DecisionEngine(
        HttpClient httpClient,
        ILogger<DecisionEngine> logger,
        IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value.OpenAi;

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
    }

    /// <summary>
    /// Ana karar metodu
    /// </summary>
    public Task<TradeSignal> AnalyzeAndDecideAsync(
        WhaleStats whaleStats,
        UserStats userStats,
        TransactionEvent transaction)
    {
        _logger.LogInformation(
            "AnalyzeAndDecideAsync çağrıldı: {Symbol} {Direction} {Amount}",
            transaction.NormalizedSymbol, transaction.Direction, transaction.UsdValue);

        var symbol = NormalizeSymbol(transaction.NormalizedSymbol);
        if (string.IsNullOrWhiteSpace(symbol) || !SupportedCoins.Contains(symbol))
        {
            return Task.FromResult(Ignore(transaction, $"Unsupported symbol: {symbol}"));
        }

        if (IsStable(symbol))
        {
            return Task.FromResult(Ignore(transaction, "Stablecoin-only movement does not change market bias."));
        }

        if (transaction.UsdValue <= 0 || userStats.TotalUsd <= 0)
        {
            return Task.FromResult(Ignore(transaction, "Missing USD value or user balance."));
        }

        var margin = CalculateMargin(whaleStats, userStats, transaction);
        if (margin <= 0 || margin < userStats.TotalUsd * 0.01m)
        {
            return Task.FromResult(Ignore(transaction, "Signal is below minimum useful account allocation."));
        }

        var isIncoming = string.Equals(transaction.Direction, "Incoming", StringComparison.OrdinalIgnoreCase);
        var isOutgoing = string.Equals(transaction.Direction, "Outgoing", StringComparison.OrdinalIgnoreCase);
        if (!isIncoming && !isOutgoing)
        {
            return Task.FromResult(Ignore(transaction, $"Unsupported direction: {transaction.Direction}"));
        }

        return Task.FromResult(new TradeSignal
        {
            Decision = "TRADE",
            Reason = isIncoming
                ? "Deterministic fallback: tracked wallet received risk asset."
                : "Deterministic fallback: tracked wallet sent/sold risk asset.",
            Symbol = symbol,
            Action = isIncoming ? TradeAction.OPEN_LONG : TradeAction.CLOSE_LONG,
            Leverage = Math.Max(1, userStats.Leverage),
            MarginAmountUSDT = Math.Round(margin, 4),
            TradeConfidence = 60,
            SourceTxHash = transaction.TxHash
        });
    }

    /// <summary>
    /// System Prompt - AI'ın kim olduğunu ve kurallarını tanımlar
    /// </summary>
    public string BuildSystemPrompt()
    {
        // ================================================================
        // SENİN VERDİĞİN SYSTEM PROMPT BURADA
        // ================================================================

        var supportedCoinsStr = string.Join(", ", SupportedCoins);

        return $@"### ROLE & CONTEXT
Sen, ""WhaleTracker"" isimli profesyonel bir kopya ticaret asistanısın.
Görevin: Bir Balina cüzdanının Spot işlemlerini analiz edip, Kullanıcı için OKX Vadeli İşlemler (Futures) tarafından mantıklı kararı vermektir.

### KRİTİK KURALLAR (INSAN GİBİ DAVRAN)
Amacımız balinayı birebir kopyalamaktır.

1.  **POZİSYON KAPATMA ÖNCELİĞİ:**
    -   Balina **SATIŞ** yaptıysa (Direction: Outgoing):
        -   Eğer Kullanıcıda o coin için açık **LONG** pozisyon varsa: Karar **KESİNLİKLE ""CLOSE_LONG""** olmalıdır (Asla OPEN_SHORT deme).
        -   Long yoksa: ""OPEN_SHORT"" diyebilirsin.
    -   Balina **ALIŞ** yaptıysa (Direction: Incoming):
        -   Eğer Kullanıcıda o coin için açık **SHORT** pozisyon varsa: Karar **KESİNLİKLE ""CLOSE_SHORT""** olmalıdır (Asla OPEN_LONG deme).
        -   Short yoksa: ""OPEN_LONG"" diyebilirsin.

2.  **KALDIRAÇ VE MARJİN:**
    -   Varsayılan Kaldıraç: user_stats içindeki leverage değerini kullan.
    -   Risk Yönetimi: Kullanıcının bakiyesini (TotalUsd) koru. 
    -   Balinanın işlem büyüklüğünün kendi portföyüne oranı neyse, kullanıcıya da aynı oranda işlem açtır.
    -   Çok küçük işlemler (Kullanıcı bakiyesinin %1'inden az) için IGNORE verebilirsin.

3.  **DESTEKLENMEYEN COİNLER:**
    -   Eğer coin aşağıdaki listede yoksa IGNORE ver.
    -   Desteklenen coinler: {supportedCoinsStr}

### GİRDİ VERİLERİ
1.  **whale_stats:** Balina portföyü.
2.  **user_stats:** Kullanıcı bakiyesi ve **AÇIK POZİSYONLARI (ActivePositions)**.
3.  **transaction_event:** Balinanın yaptığı son işlem.

### KARAR MANTIĞI
1.  İşlem Yönünü Belirle (Incoming = Alış, Outgoing = Satış).
2.  **user_stats -> ActivePositions** listesini kontrol et. Ters pozisyon var mı?
    -   Evet -> **CLOSE** komutu ver.
    -   Hayır -> **OPEN** komutu ver.
3.  Büyüklüğü Hesapla:
    -   OPEN: (TxUsd / WhaleTotal) * UserTotal
    -   CLOSE: (TxAmount / WhaleHolding) * UserPositionMargin

### ÇIKTI FORMATI (JSON)
Her zaman bu formatta JSON döndür:
{{
  ""decision"": ""TRADE"" | ""IGNORE"",
  ""reason"": ""Kısa açıklama"",
  ""symbol"": ""ETH"",
  ""action"": ""CLOSE_LONG"" | ""OPEN_LONG"" | ""OPEN_SHORT"" | ""CLOSE_SHORT"",
  ""leverage"": 2,
  ""marginAmountUSDT"": 150.00,
  ""tradeConfidence"": 95
}}";
    }

    /// <summary>
    /// User Prompt - Anlık veriyi JSON formatında hazırlar
    /// </summary>
    public Task<string> BuildUserPromptAsync(
        WhaleStats whaleStats,
        UserStats userStats,
        TransactionEvent transaction)
    {
        // ================================================================
        // VERİYİ JSON FORMATINDA HAZIRLA
        // ================================================================

        var promptData = new
        {
            whale_stats = new
            {
                TotalUsd = whaleStats.TotalUsd,
                Holdings = whaleStats.Holdings.Select(h => new
                {
                    h.Symbol,
                    h.Amount,
                    h.UsdValue
                })
            },
            user_stats = new
            {
                TotalUsd = userStats.TotalUsd,
                Leverage = userStats.Leverage,
                ActivePositions = userStats.ActivePositions.Select(p => new
                {
                    p.Symbol,
                    p.Direction,
                    p.MarginUsd,
                    p.EntryPrice
                })
            },
            transaction_event = new
            {
                Chain = transaction.Chain,
                Direction = transaction.Direction,
                TokenSymbol = transaction.TokenSymbol,
                NormalizedSymbol = transaction.NormalizedSymbol,
                Amount = transaction.Amount,
                UsdValue = transaction.UsdValue,
                BlockTimestamp = transaction.BlockTimestamp,
                TxHash = transaction.TxHash
            }
        };

        var json = JsonSerializer.Serialize(promptData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        _logger.LogDebug("User Prompt hazırlandı:\n{Json}", json);

        return Task.FromResult(json);
    }

    /// <summary>
    /// AI cevabını TradeSignal'a dönüştür
    /// </summary>
    private TradeSignal ParseAiResponse(string jsonResponse, string sourceTxHash)
    {
        using var doc = JsonDocument.Parse(jsonResponse);
        var root = doc.RootElement;

        return new TradeSignal
        {
            Decision = GetString(root, "decision", "IGNORE"),
            Reason = GetString(root, "reason", ""),
            Symbol = NormalizeSymbol(GetString(root, "symbol", "")),
            Action = GetString(root, "action", TradeAction.IGNORE),
            Leverage = GetInt(root, "leverage", 1),
            MarginAmountUSDT = GetDecimal(root, "marginAmountUSDT", 0m),
            TradeConfidence = GetInt(root, "tradeConfidence", 0),
            SourceTxHash = sourceTxHash
        };
    }

    private static TradeSignal Ignore(TransactionEvent transaction, string reason)
    {
        return new TradeSignal
        {
            Decision = "IGNORE",
            Action = TradeAction.IGNORE,
            Symbol = NormalizeSymbol(transaction.NormalizedSymbol),
            Reason = reason,
            SourceTxHash = transaction.TxHash
        };
    }

    private static decimal CalculateMargin(WhaleStats whaleStats, UserStats userStats, TransactionEvent transaction)
    {
        if (whaleStats.TotalUsd > 0)
        {
            var ratio = Math.Clamp(transaction.UsdValue / whaleStats.TotalUsd, 0m, 1m);
            return userStats.TotalUsd * ratio;
        }

        return Math.Min(userStats.TotalUsd * 0.05m, transaction.UsdValue);
    }

    private static bool IsStable(string symbol)
    {
        return symbol is "USDT" or "USDC" or "DAI" or "USDE" or "SUSDE";
    }

    private static string NormalizeSymbol(string symbol)
    {
        return symbol.ToUpperInvariant() switch
        {
            "WETH" => "ETH",
            "WBTC" => "BTC",
            "USDC" => "USDT",
            _ => symbol.ToUpperInvariant()
        };
    }

    private static string GetString(JsonElement root, string propertyName, string fallback)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static int GetInt(JsonElement root, string propertyName, int fallback)
    {
        return root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;
    }

    private static decimal GetDecimal(JsonElement root, string propertyName, decimal fallback)
    {
        return root.TryGetProperty(propertyName, out var value) && value.TryGetDecimal(out var parsed)
            ? parsed
            : fallback;
    }
}
