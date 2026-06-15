namespace WhaleTracker.API.Configuration;

public static class EnvFileLoader
{
    private static readonly Dictionary<string, string> ConfigurationKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["POSTGRES_CONNECTION"] = "ConnectionStrings__DefaultConnection",
        ["ZERION_API_KEY"] = "Zerion__ApiKey",
        ["ZERION_WHALE_ADDRESS"] = "Zerion__WhaleAddress",
        ["ETHERSCAN_API_KEY"] = "Etherscan__ApiKey",
        ["GROQ_API_KEY"] = "Groq__ApiKey",
        ["GROQ_MODEL"] = "Groq__Model",
        ["GROQ_BASE_URL"] = "Groq__BaseUrl",
        ["ALCHEMY_API_KEY"] = "Alchemy__ApiKey",
        ["ALCHEMY_NETWORK"] = "Alchemy__Network",
        ["DUNE_API_KEY"] = "Dune__ApiKey",
        ["DUNE_QUERY_ID"] = "Dune__QueryId",
        ["OPENAI_API_KEY"] = "OpenAi__ApiKey",
        ["OKX_API_KEY"] = "Okx__ApiKey",
        ["OKX_SECRET_KEY"] = "Okx__SecretKey",
        ["OKX_PASSPHRASE"] = "Okx__Passphrase",
        ["OKX_IS_DEMO"] = "Okx__IsDemo",
        ["OKX_BASE_URL"] = "Okx__BaseUrl",
        ["OKX_MARGIN_MODE"] = "Okx__MarginMode",
        ["TRADING_DEFAULT_LEVERAGE"] = "Trading__DefaultLeverage",
        ["TRADING_MIN_TRADE_SIZE"] = "Trading__MinTradeSize",
        ["TRADING_DUST_THRESHOLD_PERCENT"] = "Trading__DustThresholdPercent",
        ["ADMIN_USER"] = "Auth__AdminUser",
        ["ADMIN_PASSWORD"] = "Auth__AdminPassword",
        ["TELEGRAM_BOT_TOKEN"] = "Telegram__BotToken",
        ["TELEGRAM_CHAT_ID"] = "Telegram__ChatId",
        ["TELEGRAM_ENABLED"] = "Telegram__Enabled",
        ["HL_CONSENSUS_EXECUTION_ENABLED"] = "HyperliquidConsensusExecution__Enabled",
        ["HL_CONSENSUS_TICK_SECONDS"] = "HyperliquidConsensusExecution__TickSeconds",
        ["HL_CONSENSUS_THRESHOLD"] = "HyperliquidConsensusExecution__Threshold",
        ["HL_CONSENSUS_MULTIPLIER"] = "HyperliquidConsensusExecution__Multiplier",
        ["HL_CONSENSUS_MIN_ORDER_NOTIONAL_USD"] = "HyperliquidConsensusExecution__MinOrderNotionalUsd",
        ["HL_CONSENSUS_MIN_REBALANCE_NOTIONAL_USD"] = "HyperliquidConsensusExecution__MinRebalanceNotionalUsd",
        ["HL_CONSENSUS_LEVERAGE"] = "HyperliquidConsensusExecution__Leverage",
        ["HL_CONSENSUS_MARGIN_MODE"] = "HyperliquidConsensusExecution__MarginMode",
        ["HL_CONSENSUS_MAX_COIN_MARGIN_PCT"] = "HyperliquidConsensusExecution__MaxCoinMarginPct",
        ["HL_CONSENSUS_MAX_TOTAL_MARGIN_PCT"] = "HyperliquidConsensusExecution__MaxTotalMarginPct",
        ["HL_CONSENSUS_COIN_WEIGHT_MODE"] = "HyperliquidConsensusExecution__CoinWeightMode"
    };

    public static void LoadNearest(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        var envFiles = new Stack<string>();

        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, ".env");
            if (File.Exists(path))
            {
                envFiles.Push(path);
            }

            directory = directory.Parent;
        }

        while (envFiles.Count > 0)
        {
            Load(envFiles.Pop());
        }
    }

    private static void Load(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');

            Environment.SetEnvironmentVariable(key, value);

            if (ConfigurationKeyMap.TryGetValue(key, out var configurationKey))
            {
                Environment.SetEnvironmentVariable(configurationKey, value);
            }
        }
    }
}
