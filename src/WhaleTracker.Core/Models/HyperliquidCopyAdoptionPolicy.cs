namespace WhaleTracker.Core.Models;

public static class HyperliquidCopyAdoptionPolicy
{
    public static bool ShouldAdopt(
        bool alreadyCopied,
        bool alreadyBelowMinimum,
        bool existingMissing,
        bool existingClosed,
        bool isInitialSync,
        bool copyActiveOnEnable,
        bool adoptActiveOnlyWhenNegative,
        decimal unrealizedPnlUsd,
        bool hasNewFill)
    {
        var isNewLivePosition =
            !isInitialSync &&
            (existingMissing || existingClosed) &&
            hasNewFill;
        var canAdoptExistingPosition =
            existingMissing &&
            copyActiveOnEnable &&
            (!adoptActiveOnlyWhenNegative || unrealizedPnlUsd <= 0);

        return alreadyCopied ||
            alreadyBelowMinimum && unrealizedPnlUsd <= 0 ||
            isNewLivePosition ||
            canAdoptExistingPosition;
    }
}
