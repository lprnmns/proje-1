using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

public interface IInsiderDetectionService
{
    InsiderDetectionResult Analyze(InsiderDetectionRequest request);
}
