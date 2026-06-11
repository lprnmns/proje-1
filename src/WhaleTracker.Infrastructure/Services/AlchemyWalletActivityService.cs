using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

public class AlchemyWalletActivityService : IWalletActivityService
{
    private static readonly HashSet<string> SignalSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "USDT", "USDC", "DAI", "USDE", "SUSDE",
        "BTC", "WBTC", "ETH", "WETH", "SOL", "XRP", "BNB", "DOGE", "ADA", "LINK", "AVAX", "TON"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AlchemyWalletActivityService> _logger;
    private readonly AlchemySettings _settings;

    public AlchemyWalletActivityService(
        HttpClient httpClient,
        ILogger<AlchemyWalletActivityService> logger,
        IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value.Alchemy;
    }

    public async Task<List<TransactionEvent>> GetRecentTokenMovementsAsync(
        string walletAddress,
        string fromBlock = "0x0",
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        limit = Math.Clamp(limit, 1, 100);
        var incoming = await GetTransfersAsync(walletAddress, incoming: true, fromBlock, limit, cancellationToken);
        var outgoing = await GetTransfersAsync(walletAddress, incoming: false, fromBlock, limit, cancellationToken);

        return incoming
            .Concat(outgoing)
            .GroupBy(x => x.TxHash)
            .Select(g => g.OrderByDescending(x => x.UsdValue).First())
            .OrderByDescending(x => x.BlockTimestamp)
            .Take(limit)
            .ToList();
    }

    private async Task<List<TransactionEvent>> GetTransfersAsync(
        string walletAddress,
        bool incoming,
        string fromBlock,
        int limit,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["fromBlock"] = fromBlock,
            ["toBlock"] = "latest",
            ["category"] = new[] { "erc20" },
            ["withMetadata"] = true,
            ["excludeZeroValue"] = true,
            ["maxCount"] = $"0x{limit:x}",
            ["order"] = "desc"
        };

        parameters[incoming ? "toAddress" : "fromAddress"] = walletAddress;

        var response = await _httpClient.PostAsJsonAsync(
            BuildAlchemyUrl(),
            new
            {
                jsonrpc = "2.0",
                id = incoming ? 1 : 2,
                method = "alchemy_getAssetTransfers",
                @params = new[] { parameters }
            },
            cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Alchemy HTTP {(int)response.StatusCode}: {json}");
        }

        using var doc = JsonDocument.Parse(json);
        if (!TryGetProperty(doc.RootElement, "result", out var result) ||
            !TryGetProperty(result, "transfers", out var transfers) ||
            transfers.ValueKind != JsonValueKind.Array)
        {
            return new List<TransactionEvent>();
        }

        var events = new List<TransactionEvent>();
        foreach (var transfer in transfers.EnumerateArray())
        {
            var evt = ParseTransfer(transfer, incoming);
            if (evt != null)
            {
                events.Add(evt);
            }
        }

        _logger.LogInformation("Alchemy returned {Count} {Direction} transfers for {Wallet}", events.Count, incoming ? "incoming" : "outgoing", walletAddress);
        return events;
    }

    private static TransactionEvent? ParseTransfer(JsonElement transfer, bool incoming)
    {
        var hash = GetString(transfer, "hash");
        var asset = GetString(transfer, "asset");
        if (string.IsNullOrWhiteSpace(hash) ||
            string.IsNullOrWhiteSpace(asset) ||
            !SignalSymbols.Contains(asset))
        {
            return null;
        }

        var value = GetDecimal(transfer, "value");
        var timestampText = GetString(transfer, "metadata", "blockTimestamp");

        return new TransactionEvent
        {
            TxHash = hash,
            Chain = "ethereum",
            Direction = incoming ? "Incoming" : "Outgoing",
            TokenSymbol = asset,
            NormalizedSymbol = NormalizeSymbol(asset),
            Amount = value,
            UsdValue = EstimateUsdValue(asset, value),
            BlockTimestamp = DateTime.TryParse(timestampText, out var timestamp) ? timestamp.ToUniversalTime() : DateTime.UtcNow,
            TransactionType = "erc20_transfer"
        };
    }

    private string BuildAlchemyUrl()
    {
        return $"https://{_settings.Network}.g.alchemy.com/v2/{_settings.ApiKey}";
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Alchemy API key is not configured.");
        }
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value);
    }

    private static string GetString(JsonElement element, params string[] path)
    {
        return TryWalk(element, out var value, path)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
            : string.Empty;
    }

    private static decimal GetDecimal(JsonElement element, params string[] path)
    {
        if (!TryWalk(element, out var value, path))
        {
            return 0m;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0m
        };
    }

    private static bool TryWalk(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var segment in path)
        {
            if (!TryGetProperty(value, segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static decimal EstimateUsdValue(string symbol, decimal amount)
    {
        return symbol.ToUpperInvariant() switch
        {
            "USDC" or "USDT" or "DAI" or "USDE" => amount,
            _ => 0m
        };
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
}
