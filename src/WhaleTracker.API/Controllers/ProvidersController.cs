using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly IOkxService _okxService;
    private readonly IZerionService _zerionService;
    private readonly ITraderDiscoveryService _traderDiscoveryService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;

    public ProvidersController(
        IAIService aiService,
        IOkxService okxService,
        IZerionService zerionService,
        ITraderDiscoveryService traderDiscoveryService,
        IHttpClientFactory httpClientFactory,
        IOptions<AppSettings> settings)
    {
        _aiService = aiService;
        _okxService = okxService;
        _zerionService = zerionService;
        _traderDiscoveryService = traderDiscoveryService;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var checks = new List<object>
        {
            await CheckGroqAsync(),
            await CheckOkxAsync(),
            await CheckAlchemyAsync(),
            await CheckDuneAsync(),
            await CheckZerionAsync(),
            CheckTelegram()
        };

        return Ok(new
        {
            checkedAt = DateTime.UtcNow,
            checks
        });
    }

    private async Task<object> CheckGroqAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.Groq.ApiKey))
        {
            return Failed("groq", "not_configured");
        }

        var result = await _aiService.TestConnectionAsync();
        return result.success
            ? Ok("groq", new { model = _settings.Groq.Model })
            : Failed("groq", result.message);
    }

    private async Task<object> CheckOkxAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.Okx.ApiKey) ||
            string.IsNullOrWhiteSpace(_settings.Okx.SecretKey) ||
            string.IsNullOrWhiteSpace(_settings.Okx.Passphrase))
        {
            return Failed("okx", "not_configured");
        }

        try
        {
            var account = await _okxService.GetAccountInfoAsync();
            return Ok("okx", new
            {
                isDemo = _settings.Okx.IsDemo,
                totalUsd = account.TotalUsd,
                positions = account.ActivePositions.Count
            });
        }
        catch (Exception ex)
        {
            return Failed("okx", ex.Message);
        }
    }

    private async Task<object> CheckAlchemyAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.Alchemy.ApiKey))
        {
            return Failed("alchemy", "not_configured");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(
                $"https://{_settings.Alchemy.Network}.g.alchemy.com/v2/{_settings.Alchemy.ApiKey}",
                new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "eth_blockNumber",
                    @params = Array.Empty<object>()
                });

            var body = await response.Content.ReadFromJsonAsync<AlchemyRpcResponse>();
            return response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(body?.Result)
                ? Ok("alchemy", new { network = _settings.Alchemy.Network, latestBlockHex = body.Result })
                : Failed("alchemy", $"http_{(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return Failed("alchemy", ex.Message);
        }
    }

    private async Task<object> CheckZerionAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.Zerion.ApiKey))
        {
            return Failed("zerion", "not_configured");
        }

        try
        {
            var address = string.IsNullOrWhiteSpace(_settings.Zerion.WhaleAddress) ||
                          _settings.Zerion.WhaleAddress.Contains("BALINA", StringComparison.OrdinalIgnoreCase)
                ? "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045"
                : _settings.Zerion.WhaleAddress;

            var portfolio = await _zerionService.GetWalletPortfolioAsync(address);
            return Ok("zerion", new { hasPortfolio = portfolio.TotalUsd >= 0, holdings = portfolio.Holdings.Count });
        }
        catch (Exception ex)
        {
            return Failed("zerion", ex.Message);
        }
    }

    private async Task<object> CheckDuneAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.Dune.ApiKey))
        {
            return Failed("dune", "not_configured");
        }

        try
        {
            var ok = await _traderDiscoveryService.CheckHealthAsync();
            return ok
                ? Ok("dune", new { mode = _settings.Dune.QueryId.HasValue ? "saved_query" : "sql" })
                : Failed("dune", "health_check_failed");
        }
        catch (Exception ex)
        {
            return Failed("dune", ex.Message);
        }
    }

    private object CheckTelegram()
    {
        if (!_settings.Telegram.Enabled)
        {
            return Failed("telegram", "disabled");
        }

        if (string.IsNullOrWhiteSpace(_settings.Telegram.BotToken) ||
            string.IsNullOrWhiteSpace(_settings.Telegram.ChatId))
        {
            return Failed("telegram", "not_configured");
        }

        return Ok("telegram", new { configured = true });
    }

    private static object Ok(string provider, object data)
    {
        return new { provider, configured = true, ok = true, data };
    }

    private static object Failed(string provider, string message)
    {
        return new { provider, configured = message != "not_configured", ok = false, message };
    }

    private sealed class AlchemyRpcResponse
    {
        public string? Result { get; set; }
    }
}
