using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

public interface IHyperliquidConsensusExecutionService
{
    Task<HyperliquidConsensusExecutionPlan> BuildPlanAsync(CancellationToken cancellationToken = default);

    Task<HyperliquidConsensusApplyResult> ApplyPlanAsync(CancellationToken cancellationToken = default);
}
