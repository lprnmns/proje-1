using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

public class TelegramNotificationService : INotificationService
{
    private static DateTime _mutedUntilUtc = DateTime.MinValue;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly TelegramSettings _settings;

    public TelegramNotificationService(
        HttpClient httpClient,
        ILogger<TelegramNotificationService> logger,
        IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value.Telegram;
    }

    public async Task SendAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled ||
            string.IsNullOrWhiteSpace(_settings.BotToken) ||
            string.IsNullOrWhiteSpace(_settings.ChatId) ||
            DateTime.UtcNow < _mutedUntilUtc ||
            !IsAllowedNotification(title))
        {
            return;
        }

        try
        {
            var text = $"*{Escape(title)}*\n{Escape(message)}";
            var response = await _httpClient.PostAsJsonAsync(
                $"https://api.telegram.org/bot{_settings.BotToken}/sendMessage",
                new
                {
                    chat_id = _settings.ChatId,
                    text,
                    parse_mode = "MarkdownV2",
                    disable_web_page_preview = true
                },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (body.Contains("chat not found", StringComparison.OrdinalIgnoreCase))
                {
                    _mutedUntilUtc = DateTime.UtcNow.AddMinutes(5);
                }

                _logger.LogWarning("Telegram notification failed: {Status} {Body}", response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram notification could not be sent.");
        }
    }

    private static bool IsAllowedNotification(string title) =>
        title.StartsWith("Consensus threshold", StringComparison.OrdinalIgnoreCase) ||
        title.StartsWith("WhaleTracker login", StringComparison.OrdinalIgnoreCase);

    private static string Escape(string value)
    {
        var chars = new[] { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" };
        foreach (var ch in chars)
        {
            value = value.Replace(ch, "\\" + ch, StringComparison.Ordinal);
        }

        return value;
    }
}
