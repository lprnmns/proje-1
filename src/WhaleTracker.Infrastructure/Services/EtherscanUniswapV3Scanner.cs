using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

public class EtherscanUniswapV3Scanner : IHistoricalSwapScanner
{
    private const string SwapTopic = "0xc42079f94a6350d7e6235f29174924f928cc2ac818eb64fed8004e115fbcca67";

    private static readonly IReadOnlyList<PoolDefinition> Pools = new[]
    {
        new PoolDefinition(
            Name: "WETH_USDC_0.05",
            Address: "0x88e6a0c2ddd26feeb64f039a2c41296fcb3f5640",
            Token0Symbol: "USDC",
            Token0Decimals: 6,
            Token1Symbol: "WETH",
            Token1Decimals: 18),
        new PoolDefinition(
            Name: "WETH_USDT_0.05",
            Address: "0x11b81a04b168c65f79b691045db47ad0433a43d6",
            Token0Symbol: "WETH",
            Token0Decimals: 18,
            Token1Symbol: "USDT",
            Token1Decimals: 6),
        new PoolDefinition(
            Name: "WBTC_USDC_0.3",
            Address: "0x99ac8cA7087fA4A2A1FB6357269965A2014ABc35",
            Token0Symbol: "WBTC",
            Token0Decimals: 8,
            Token1Symbol: "USDC",
            Token1Decimals: 6),
        new PoolDefinition(
            Name: "WBTC_USDT_0.3",
            Address: "0x9db9e0e53058c89e5b94e29621a205198648425b",
            Token0Symbol: "WBTC",
            Token0Decimals: 8,
            Token1Symbol: "USDT",
            Token1Decimals: 6),
        new PoolDefinition(
            Name: "WBTC_USDT_0.05",
            Address: "0x56534741CD8B152df6d48AdF7ac51f75169A83b2",
            Token0Symbol: "WBTC",
            Token0Decimals: 8,
            Token1Symbol: "USDT",
            Token1Decimals: 6),
        new PoolDefinition(
            Name: "LINK_USDC_0.3",
            Address: "0xfad57d2039c21811c8f2b5d5b65308aa99d31559",
            Token0Symbol: "LINK",
            Token0Decimals: 18,
            Token1Symbol: "USDC",
            Token1Decimals: 6)
    };

    private readonly HttpClient _httpClient;
    private readonly IInsiderDetectionService _detector;
    private readonly ILogger<EtherscanUniswapV3Scanner> _logger;
    private readonly EtherscanSettings _settings;
    private readonly SemaphoreSlim _rateLimitLock = new(1, 1);

    public EtherscanUniswapV3Scanner(
        HttpClient httpClient,
        IInsiderDetectionService detector,
        ILogger<EtherscanUniswapV3Scanner> logger,
        IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _detector = detector;
        _logger = logger;
        _settings = settings.Value.Etherscan;
    }

    public async Task<InsiderDetectionResult> ScanUniswapV3Async(
        InsiderDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var preStart = await GetBlockByTimestampAsync(request.PreCrashStartUtc, "after", cancellationToken);
        var preEnd = await GetBlockByTimestampAsync(request.PreCrashEndUtc, "before", cancellationToken);
        var dipStart = await GetBlockByTimestampAsync(request.DipBuyStartUtc, "after", cancellationToken);
        var dipEnd = await GetBlockByTimestampAsync(request.DipBuyEndUtc, "before", cancellationToken);

        var swaps = new List<HistoricalSwap>();
        foreach (var pool in Pools)
        {
            swaps.AddRange(await GetPoolSwapsAsync(pool, preStart, preEnd, request.PreCrashStartUtc, cancellationToken));
            swaps.AddRange(await GetPoolSwapsAsync(pool, dipStart, dipEnd, request.DipBuyStartUtc, cancellationToken));
        }

        request.Swaps = swaps;
        return _detector.Analyze(request);
    }

    private async Task<int> GetBlockByTimestampAsync(DateTime timestampUtc, string closest, CancellationToken cancellationToken)
    {
        var query = BuildQuery(new Dictionary<string, string>
        {
            ["module"] = "block",
            ["action"] = "getblocknobytime",
            ["timestamp"] = new DateTimeOffset(timestampUtc.ToUniversalTime()).ToUnixTimeSeconds().ToString(),
            ["closest"] = closest
        });

        using var doc = JsonDocument.Parse(await GetAsync(query, cancellationToken));
        var result = GetString(doc.RootElement, "result");
        return int.TryParse(result, out var block) ? block : throw new InvalidOperationException($"Etherscan block lookup failed: {result}");
    }

    private async Task<List<HistoricalSwap>> GetPoolSwapsAsync(
        PoolDefinition pool,
        int fromBlock,
        int toBlock,
        DateTime fallbackTimestampUtc,
        CancellationToken cancellationToken)
    {
        var output = new List<HistoricalSwap>();
        var ranges = new Queue<(int From, int To)>();
        ranges.Enqueue((fromBlock, toBlock));

        while (ranges.Count > 0)
        {
            var (from, to) = ranges.Dequeue();
            var query = BuildQuery(new Dictionary<string, string>
            {
                ["module"] = "logs",
                ["action"] = "getLogs",
                ["fromBlock"] = from.ToString(),
                ["toBlock"] = to.ToString(),
                ["address"] = pool.Address,
                ["topic0"] = SwapTopic
            });

            using var doc = JsonDocument.Parse(await GetAsync(query, cancellationToken));
            if (!TryGetProperty(doc.RootElement, "result", out var result) || result.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var logs = result.EnumerateArray().ToList();
            if (logs.Count >= 1000 && to - from > 2)
            {
                var mid = (from + to) / 2;
                ranges.Enqueue((from, mid));
                ranges.Enqueue((mid + 1, to));
                continue;
            }

            foreach (var log in logs)
            {
                var swap = ParseSwap(pool, log, fallbackTimestampUtc);
                if (swap != null)
                {
                    output.Add(swap);
                }
            }
        }

        _logger.LogInformation("Parsed {Count} swaps for {Pool}", output.Count, pool.Name);
        return output;
    }

    private static HistoricalSwap? ParseSwap(PoolDefinition pool, JsonElement log, DateTime fallbackTimestampUtc)
    {
        var data = GetString(log, "data");
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        data = data.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? data[2..] : data;
        if (data.Length < 128)
        {
            return null;
        }

        var amount0 = DecodeInt256(data[..64]);
        var amount1 = DecodeInt256(data.Substring(64, 64));
        var token0Amount = ToDecimal(BigInteger.Abs(amount0), pool.Token0Decimals);
        var token1Amount = ToDecimal(BigInteger.Abs(amount1), pool.Token1Decimals);

        var tokenInSymbol = amount0 > 0 ? pool.Token0Symbol : pool.Token1Symbol;
        var tokenOutSymbol = amount0 > 0 ? pool.Token1Symbol : pool.Token0Symbol;
        var tokenInAmount = amount0 > 0 ? token0Amount : token1Amount;
        var tokenOutAmount = amount0 > 0 ? token1Amount : token0Amount;
        var usdValue = IsStable(tokenInSymbol) ? tokenInAmount : IsStable(tokenOutSymbol) ? tokenOutAmount : 0m;

        if (usdValue <= 0)
        {
            return null;
        }

        return new HistoricalSwap
        {
            WalletAddress = ExtractRecipient(log),
            TxHash = GetString(log, "transactionHash"),
            TimestampUtc = ParseTimestamp(log, fallbackTimestampUtc),
            TokenInSymbol = tokenInSymbol,
            TokenInAmount = tokenInAmount,
            TokenOutSymbol = tokenOutSymbol,
            TokenOutAmount = tokenOutAmount,
            UsdValue = usdValue
        };
    }

    private async Task<string> GetAsync(string query, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await _rateLimitLock.WaitAsync(cancellationToken);
            try
            {
                await Task.Delay(400, cancellationToken);
                var response = await _httpClient.GetAsync($"{_settings.BaseUrl}?{query}", cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Etherscan HTTP {(int)response.StatusCode}: {body}");
                }

                if (body.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                    body.Contains("Max calls per sec", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Etherscan rate limit on attempt {Attempt}/5", attempt);
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
                    continue;
                }

                return body;
            }
            finally
            {
                _rateLimitLock.Release();
            }
        }

        throw new InvalidOperationException("Etherscan request failed after retries.");
    }

    private string BuildQuery(Dictionary<string, string> values)
    {
        values["chainid"] = _settings.ChainId;
        values["apikey"] = _settings.ApiKey;
        return string.Join("&", values.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Etherscan API key is not configured.");
        }
    }

    private static BigInteger DecodeInt256(string hex)
    {
        var unsigned = BigInteger.Parse("00" + hex, System.Globalization.NumberStyles.HexNumber);
        var maxInt256 = BigInteger.One << 255;
        var modulo = BigInteger.One << 256;
        return unsigned >= maxInt256 ? unsigned - modulo : unsigned;
    }

    private static decimal ToDecimal(BigInteger value, int decimals)
    {
        var divisor = BigInteger.Pow(10, decimals);
        var whole = value / divisor;
        var remainder = value % divisor;
        return (decimal)whole + (decimal)remainder / (decimal)divisor;
    }

    private static string ExtractRecipient(JsonElement log)
    {
        if (!TryGetProperty(log, "topics", out var topics) || topics.ValueKind != JsonValueKind.Array || topics.GetArrayLength() < 3)
        {
            return string.Empty;
        }

        var topic = topics[2].GetString() ?? string.Empty;
        return topic.Length >= 40 ? "0x" + topic[^40..] : string.Empty;
    }

    private static DateTime ParseTimestamp(JsonElement log, DateTime fallback)
    {
        var raw = GetString(log, "timeStamp");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback.ToUniversalTime();
        }

        var seconds = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(raw, 16)
            : long.Parse(raw);
        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value);
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
            : string.Empty;
    }

    private static bool IsStable(string symbol)
    {
        return symbol.Equals("USDC", StringComparison.OrdinalIgnoreCase) ||
               symbol.Equals("USDT", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PoolDefinition(
        string Name,
        string Address,
        string Token0Symbol,
        int Token0Decimals,
        string Token1Symbol,
        int Token1Decimals);
}
