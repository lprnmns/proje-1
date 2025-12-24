using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

/// <summary>
/// Groq AI Servisi
/// Balina hareketlerini analiz edip işlem kararı verir
/// 
/// Groq API: https://console.groq.com/docs/api-reference
/// </summary>
public class GroqService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqService> _logger;
    private readonly GroqSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GroqService(
        HttpClient httpClient,
        ILogger<GroqService> logger,
        IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value.Groq;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
    }

    // ================================================================
    // ANA ANALİZ METODU
    // ================================================================

    /// <summary>
    /// Balina hareketini analiz et ve işlem kararı ver
    /// </summary>
    public async Task<AIDecision> AnalyzeMovementAsync(AIContext context)
    {
        _logger.LogInformation("🤖 AI Analiz başlıyor: {Type} {Symbol} ${Value}",
            context.NewMovement.Type, context.NewMovement.Symbol, context.NewMovement.ValueUSDT);

        var decision = new AIDecision();

        try
        {
            // 1. Prompt oluştur
            var prompt = BuildAnalysisPrompt(context);

            _logger.LogDebug("📝 Prompt:\n{Prompt}", prompt);

            // 2. AI'a gönder
            var response = await SendChatRequestAsync(prompt);
            
            decision.RawResponse = response;
            _logger.LogDebug("🤖 AI Raw Response:\n{Response}", response);

            // 3. Yanıtı parse et
            decision = ParseAIResponse(response);

            _logger.LogInformation(
                "🎯 AI Karar: {Action} {Symbol} ${Amount} (Güven: {Confidence}%)",
                decision.Action, decision.Symbol, decision.AmountUSDT, decision.ConfidenceScore);

            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Analiz hatası!");
            decision.ParseSuccess = false;
            decision.ParseError = ex.Message;
            decision.Action = "IGNORE";
            decision.Reasoning = $"AI analiz hatası: {ex.Message}";
            return decision;
        }
    }

    // ================================================================
    // PROMPT BUILDER
    // ================================================================

    private string BuildAnalysisPrompt(AIContext context)
    {
        var sb = new StringBuilder();

        // System context
        sb.AppendLine("Sen bir kripto copy-trading botusun. Bir balina cüzdanının hareketlerini BİREBİR kopyalıyoruz.");
        sb.AppendLine("Balina bir coin aldığında biz de OKX Futures'ta LONG açıyoruz.");
        sb.AppendLine("Balina bir coin sattığında biz de mevcut LONG pozisyonumuzu kapatıyoruz (CLOSE_LONG sinyali).");
        sb.AppendLine("AMAÇ: Balinayı TAMAMEN kopyalamak. Aynı oran, aynı token.");
        sb.AppendLine();

        // Durum bilgisi
        sb.AppendLine("=== MEVCUT DURUM ===");
        sb.AppendLine($"Bizim Bakiye: ${context.OurBalanceUSDT:F2} USDT");
        sb.AppendLine($"Balina Bakiye: ${context.WhaleBalanceUSDT:F2} USDT");
        
        // Oran hesapla
        var whalePercentage = context.WhaleBalanceUSDT > 0 
            ? (context.NewMovement.ValueUSDT / context.WhaleBalanceUSDT) * 100 
            : 0;
        var ourAmount = context.OurBalanceUSDT * (whalePercentage / 100);
        sb.AppendLine($"Balina bu işlemde portföyünün %{whalePercentage:F2}'sini kullandı");
        sb.AppendLine($"Biz de aynı oranda: ${ourAmount:F2} USDT kullanmalıyız");
        sb.AppendLine();

        // Bizim pozisyonlarımız
        sb.AppendLine("=== BİZİM POZİSYONLARIMIZ ===");
        if (context.OurPositions.Any())
        {
            foreach (var pos in context.OurPositions)
            {
                sb.AppendLine($"- {pos.Symbol} {pos.Direction}: ${pos.MarginUSDT:F2} margin");
            }
        }
        else
        {
            sb.AppendLine("- Açık pozisyon yok");
        }
        sb.AppendLine();

        // Yeni hareket
        sb.AppendLine("=== YENİ BALİNA HAREKETİ ===");
        sb.AppendLine($"Tip: {context.NewMovement.Type}");
        sb.AppendLine($"Token: {context.NewMovement.Symbol}");
        sb.AppendLine($"Miktar: {context.NewMovement.Amount:F4} {context.NewMovement.Symbol}");
        sb.AppendLine($"Değer: ${context.NewMovement.ValueUSDT:F2} USDT");
        sb.AppendLine($"Balina Portföy Oranı: %{whalePercentage:F2}");
        sb.AppendLine();

        // Karar istemi - basitleştirilmiş
        sb.AppendLine("=== KARAR VER ===");
        sb.AppendLine("Aşağıdaki formatta SADECE JSON döndür:");
        sb.AppendLine();
        sb.AppendLine(@"{
  ""action"": ""LONG"" veya ""CLOSE_LONG"" veya ""IGNORE"",
  ""symbol"": ""TOKEN_SEMBOLU"",
  ""amount_usdt"": SAYI,
  ""reasoning"": ""Kısa açıklama""
}");
        sb.AppendLine();
        sb.AppendLine("KURALLAR:");
        sb.AppendLine("1. Balina BUY yaptıysa -> LONG aç (aynı token)");
        sb.AppendLine("2. Balina SELL yaptıysa -> CLOSE_LONG (mevcut LONG pozisyonu kapat)");
        sb.AppendLine($"3. amount_usdt = ${ourAmount:F2} (balina ile AYNI ORAN)");
        sb.AppendLine("4. leverage ve confidence YAZMA, biz sabit 3x kullanıyoruz");
        sb.AppendLine("5. SADECE JSON döndür, başka bir şey yazma!");
        sb.AppendLine("6. Minimum kontrol YAPMA - borsadaki gerçek limitler ayrıca kontrol edilecek");

        return sb.ToString();
    }

    // ================================================================
    // AI RESPONSE PARSER
    // ================================================================

    private AIDecision ParseAIResponse(string response)
    {
        var decision = new AIDecision
        {
            RawResponse = response
        };

        try
        {
            // JSON'u bul (bazen AI ekstra text ekleyebilir)
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}", RegexOptions.Multiline);
            
            if (!jsonMatch.Success)
            {
                decision.ParseSuccess = false;
                decision.ParseError = "JSON bulunamadı";
                decision.Action = "IGNORE";
                return decision;
            }

            var jsonStr = jsonMatch.Value;
            var parsed = JsonSerializer.Deserialize<AIResponseJson>(jsonStr, JsonOptions);

            if (parsed == null)
            {
                decision.ParseSuccess = false;
                decision.ParseError = "JSON parse edilemedi";
                decision.Action = "IGNORE";
                return decision;
            }

            // Map to AIDecision
            var action = parsed.Action?.Trim().ToUpperInvariant() ?? "IGNORE";
            action = action switch
            {
                "SHORT" => "CLOSE_LONG",
                "SELL" => "CLOSE_LONG",
                "CLOSE" => "CLOSE_LONG",
                _ => action
            };

            decision.Action = action;
            decision.Symbol = parsed.Symbol?.ToUpper() ?? "";
            decision.AmountUSDT = parsed.AmountUsdt;
            decision.Leverage = 3;  // SABİT 3x KALDIRAÇ
            decision.ConfidenceScore = 100; // Güven skoru kullanılmıyor, sabit 100
            decision.Reasoning = parsed.Reasoning ?? "";
            decision.ParseSuccess = true;

            // Validasyon
            if (decision.Action == "LONG" || decision.Action == "CLOSE_LONG")
            {
                decision.ShouldTrade = true;

                if (string.IsNullOrEmpty(decision.Symbol))
                {
                    decision.ShouldTrade = false;
                    decision.Action = "IGNORE";
                    decision.Reasoning = "Symbol belirtilmedi";
                }
                else if (decision.AmountUSDT <= 0)
                {
                    decision.ShouldTrade = false;
                    decision.Action = "IGNORE";
                    decision.Reasoning = "Miktar 0 veya negatif";
                }
            }

            return decision;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI yanıt parse hatası: {Response}", response);
            decision.ParseSuccess = false;
            decision.ParseError = ex.Message;
            decision.Action = "IGNORE";
            return decision;
        }
    }

    // ================================================================
    // API METODLARI
    // ================================================================

    /// <summary>
    /// API bağlantısını test et
    /// </summary>
    public async Task<(bool success, string message)> TestConnectionAsync()
    {
        try
        {
            _logger.LogInformation("🔌 Groq API bağlantı testi...");

            var response = await AskAsync("Say 'OK' if you can hear me.");

            if (!string.IsNullOrEmpty(response))
            {
                _logger.LogInformation("✅ Groq API bağlantısı başarılı");
                return (true, $"Bağlantı başarılı. Model: {_settings.Model}");
            }

            return (false, "Boş yanıt alındı");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Groq API bağlantı hatası");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Basit soru sor
    /// </summary>
    public async Task<string> AskAsync(string question)
    {
        return await SendChatRequestAsync(question);
    }

    /// <summary>
    /// Chat completion isteği gönder
    /// </summary>
    private async Task<string> SendChatRequestAsync(string prompt)
    {
        var requestBody = new
        {
            model = _settings.Model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            max_tokens = _settings.MaxTokens,
            temperature = (double)_settings.Temperature
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("POST /chat/completions: {Body}", json);

        var response = await _httpClient.PostAsync("/openai/v1/chat/completions", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        _logger.LogDebug("Response: {Content}", responseContent);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Groq API hatası: {Status} - {Content}", response.StatusCode, responseContent);
            throw new HttpRequestException($"Groq API Error: {response.StatusCode} - {responseContent}");
        }

        var result = JsonSerializer.Deserialize<GroqChatResponse>(responseContent, JsonOptions);
        
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
    }
}

// ================================================================
// GROQ API DTOs
// ================================================================

internal class AIResponseJson
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("amount_usdt")]
    public decimal AmountUsdt { get; set; }

    [JsonPropertyName("leverage")]
    public int Leverage { get; set; }

    [JsonPropertyName("confidence")]
    public int Confidence { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }
}

internal class GroqChatResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public List<GroqChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public GroqUsage? Usage { get; set; }
}

internal class GroqChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public GroqMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class GroqMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

internal class GroqUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
