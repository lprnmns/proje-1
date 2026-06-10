namespace WhaleTracker.Core.Models;

public class InsiderDetectionRequest
{
    public DateTime PreCrashStartUtc { get; set; }
    public DateTime PreCrashEndUtc { get; set; }
    public DateTime DipBuyStartUtc { get; set; }
    public DateTime DipBuyEndUtc { get; set; }
    public decimal MinimumProfitUsd { get; set; } = 1000m;
    public List<HistoricalSwap> Swaps { get; set; } = new();
}

public class HistoricalSwap
{
    public string WalletAddress { get; set; } = string.Empty;
    public string TxHash { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string TokenInSymbol { get; set; } = string.Empty;
    public decimal TokenInAmount { get; set; }
    public string TokenOutSymbol { get; set; } = string.Empty;
    public decimal TokenOutAmount { get; set; }
    public decimal UsdValue { get; set; }
}

public class InsiderCandidate
{
    public string WalletAddress { get; set; } = string.Empty;
    public string AssetSymbol { get; set; } = string.Empty;
    public decimal EstimatedProfitUsd { get; set; }
    public decimal MatchedAssetAmount { get; set; }
    public decimal AverageSellPriceUsd { get; set; }
    public decimal AverageBuyPriceUsd { get; set; }
    public decimal InsiderScore { get; set; }
    public decimal TimingScore { get; set; }
    public decimal SizeScore { get; set; }
    public decimal ProfitScore { get; set; }
    public List<string> EvidenceTxHashes { get; set; } = new();
}

public class InsiderDetectionResult
{
    public int ScannedSwapCount { get; set; }
    public int CandidateCount { get; set; }
    public List<InsiderCandidate> Candidates { get; set; } = new();
}
