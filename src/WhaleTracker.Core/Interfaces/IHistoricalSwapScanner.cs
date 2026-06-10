using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

public interface IHistoricalSwapScanner
{
    Task<InsiderDetectionResult> ScanUniswapV3Async(InsiderDetectionRequest request, CancellationToken cancellationToken = default);
}
