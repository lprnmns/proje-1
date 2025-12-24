namespace WhaleTracker.Core.Models;

/// <summary>
/// Uygulama ayarları
/// appsettings.json'dan okunur
/// </summary>
public class AppSettings
{
    public ZerionSettings Zerion { get; set; } = new();
    public OkxSettings Okx { get; set; } = new();
    public OpenAiSettings OpenAi { get; set; } = new();
    public GroqSettings Groq { get; set; } = new();
    public TradingSettings Trading { get; set; } = new();
}

public class ZerionSettings
{
    /// <summary>
    /// Zerion API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Takip edilecek balina cüzdan adresi
    /// </summary>
    public string WhaleAddress { get; set; } = string.Empty;

    /// <summary>
    /// Polling aralığı (saniye)
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;
}

public class OkxSettings
{
    /// <summary>
    /// OKX API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OKX Secret Key
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// OKX Passphrase
    /// </summary>
    public string Passphrase { get; set; } = string.Empty;

    /// <summary>
    /// Demo mod mu? (true = paper trading)
    /// </summary>
    public bool IsDemo { get; set; } = true;

    /// <summary>
    /// Base URL (demo veya gerçek)
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.okx.com";
}

public class OpenAiSettings
{
    /// <summary>
    /// OpenAI API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Kullanılacak model (gpt-4, gpt-3.5-turbo vs.)
    /// </summary>
    public string Model { get; set; } = "gpt-4";
}

/// <summary>
/// Groq API Ayarları
/// </summary>
public class GroqSettings
{
    /// <summary>
    /// Groq API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Kullanılacak model
    /// </summary>
    public string Model { get; set; } = "openai/gpt-oss-120b";

    /// <summary>
    /// Base URL
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    /// <summary>
    /// Reasoning effort (low, medium, high)
    /// </summary>
    public string ReasoningEffort { get; set; } = "high";

    /// <summary>
    /// Max tokens
    /// </summary>
    public int MaxTokens { get; set; } = 1000;

    /// <summary>
    /// Temperature (0-1)
    /// </summary>
    public decimal Temperature { get; set; } = 0.1m;
}

public class TradingSettings
{
    /// <summary>
    /// Varsayılan kaldıraç
    /// </summary>
    public int DefaultLeverage { get; set; } = 2;

    /// <summary>
    /// Minimum işlem büyüklüğü (USDT)
    /// </summary>
    public decimal MinTradeSize { get; set; } = 10m;

    /// <summary>
    /// Toz temizleme eşiği (% olarak)
    /// </summary>
    public decimal DustThresholdPercent { get; set; } = 95m;
}
