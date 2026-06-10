using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

public class InsiderDetectionService : IInsiderDetectionService
{
    private static readonly HashSet<string> StableSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "USDT", "USDC", "DAI", "USDE", "SUSDE", "FDUSD", "TUSD", "PYUSD"
    };

    public InsiderDetectionResult Analyze(InsiderDetectionRequest request)
    {
        var preSells = request.Swaps
            .Where(x => IsInWindow(x.TimestampUtc, request.PreCrashStartUtc, request.PreCrashEndUtc))
            .Where(IsRiskToStable)
            .GroupBy(x => (Wallet: NormalizeWallet(x.WalletAddress), Asset: NormalizeAsset(x.TokenInSymbol)))
            .ToDictionary(g => g.Key, g => g.ToList());

        var dipBuys = request.Swaps
            .Where(x => IsInWindow(x.TimestampUtc, request.DipBuyStartUtc, request.DipBuyEndUtc))
            .Where(IsStableToRisk)
            .GroupBy(x => (Wallet: NormalizeWallet(x.WalletAddress), Asset: NormalizeAsset(x.TokenOutSymbol)))
            .ToDictionary(g => g.Key, g => g.ToList());

        var candidates = new List<InsiderCandidate>();
        foreach (var key in preSells.Keys.Intersect(dipBuys.Keys))
        {
            var sells = preSells[key];
            var buys = dipBuys[key];

            var soldAmount = sells.Sum(x => x.TokenInAmount);
            var boughtAmount = buys.Sum(x => x.TokenOutAmount);
            if (soldAmount <= 0 || boughtAmount <= 0)
            {
                continue;
            }

            var sellUsd = sells.Sum(x => x.UsdValue);
            var buyUsd = buys.Sum(x => x.UsdValue);
            var avgSellPrice = sellUsd / soldAmount;
            var avgBuyPrice = buyUsd / boughtAmount;
            var matchedAmount = Math.Min(soldAmount, boughtAmount);
            var profit = matchedAmount * (avgSellPrice - avgBuyPrice);

            if (profit < request.MinimumProfitUsd)
            {
                continue;
            }

            var timingScore = CalculateTimingScore(sells, request.PreCrashEndUtc, buys, request.DipBuyStartUtc);
            var sizeScore = Clamp01((sellUsd + buyUsd) / 1_000_000m) * 100m;
            var profitScore = Clamp01(profit / 250_000m) * 100m;
            var insiderScore = Math.Round((timingScore * 0.35m) + (sizeScore * 0.25m) + (profitScore * 0.40m), 2);

            candidates.Add(new InsiderCandidate
            {
                WalletAddress = key.Wallet,
                AssetSymbol = key.Asset,
                EstimatedProfitUsd = Math.Round(profit, 2),
                MatchedAssetAmount = Math.Round(matchedAmount, 8),
                AverageSellPriceUsd = Math.Round(avgSellPrice, 8),
                AverageBuyPriceUsd = Math.Round(avgBuyPrice, 8),
                TimingScore = Math.Round(timingScore, 2),
                SizeScore = Math.Round(sizeScore, 2),
                ProfitScore = Math.Round(profitScore, 2),
                InsiderScore = insiderScore,
                EvidenceTxHashes = sells.Concat(buys).Select(x => x.TxHash).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList()
            });
        }

        var ordered = candidates
            .OrderByDescending(x => x.InsiderScore)
            .ThenByDescending(x => x.EstimatedProfitUsd)
            .ToList();

        return new InsiderDetectionResult
        {
            ScannedSwapCount = request.Swaps.Count,
            CandidateCount = ordered.Count,
            Candidates = ordered
        };
    }

    private static bool IsRiskToStable(HistoricalSwap swap)
    {
        return !StableSymbols.Contains(NormalizeAsset(swap.TokenInSymbol)) &&
               StableSymbols.Contains(NormalizeAsset(swap.TokenOutSymbol)) &&
               swap.TokenInAmount > 0 &&
               swap.UsdValue > 0;
    }

    private static bool IsStableToRisk(HistoricalSwap swap)
    {
        return StableSymbols.Contains(NormalizeAsset(swap.TokenInSymbol)) &&
               !StableSymbols.Contains(NormalizeAsset(swap.TokenOutSymbol)) &&
               swap.TokenOutAmount > 0 &&
               swap.UsdValue > 0;
    }

    private static decimal CalculateTimingScore(
        List<HistoricalSwap> sells,
        DateTime preCrashEndUtc,
        List<HistoricalSwap> buys,
        DateTime dipBuyStartUtc)
    {
        var lastSellMinutesBeforeCrash = Math.Max(0, (preCrashEndUtc - sells.Max(x => x.TimestampUtc)).TotalMinutes);
        var firstBuyMinutesAfterDipStart = Math.Max(0, (buys.Min(x => x.TimestampUtc) - dipBuyStartUtc).TotalMinutes);
        var sellTiming = Clamp01(1m - (decimal)lastSellMinutesBeforeCrash / 360m) * 100m;
        var buyTiming = Clamp01(1m - (decimal)firstBuyMinutesAfterDipStart / 360m) * 100m;
        return (sellTiming + buyTiming) / 2m;
    }

    private static bool IsInWindow(DateTime value, DateTime start, DateTime end)
    {
        var utcValue = value.ToUniversalTime();
        return utcValue >= start.ToUniversalTime() && utcValue <= end.ToUniversalTime();
    }

    private static decimal Clamp01(decimal value)
    {
        return Math.Max(0m, Math.Min(1m, value));
    }

    private static string NormalizeWallet(string walletAddress)
    {
        return walletAddress.Trim().ToLowerInvariant();
    }

    private static string NormalizeAsset(string symbol)
    {
        return symbol.Trim().ToUpperInvariant() switch
        {
            "WETH" => "ETH",
            "WBTC" => "BTC",
            _ => symbol.Trim().ToUpperInvariant()
        };
    }
}
