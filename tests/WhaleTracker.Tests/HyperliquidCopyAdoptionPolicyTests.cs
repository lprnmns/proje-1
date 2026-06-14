using WhaleTracker.Core.Models;
using Xunit;

namespace WhaleTracker.Tests;

public class HyperliquidCopyAdoptionPolicyTests
{
    [Fact]
    public void ExistingUntrackedProfitablePosition_IsNotMistakenForNewPosition()
    {
        var result = HyperliquidCopyAdoptionPolicy.ShouldAdopt(
            alreadyCopied: false,
            alreadyBelowMinimum: false,
            existingMissing: true,
            existingClosed: false,
            isInitialSync: false,
            copyActiveOnEnable: true,
            adoptActiveOnlyWhenNegative: true,
            unrealizedPnlUsd: 100m,
            hasNewFill: false);

        Assert.False(result);
    }

    [Fact]
    public void ExistingUntrackedLosingPosition_CanBeAdopted()
    {
        var result = HyperliquidCopyAdoptionPolicy.ShouldAdopt(
            alreadyCopied: false,
            alreadyBelowMinimum: false,
            existingMissing: true,
            existingClosed: false,
            isInitialSync: false,
            copyActiveOnEnable: true,
            adoptActiveOnlyWhenNegative: true,
            unrealizedPnlUsd: -100m,
            hasNewFill: false);

        Assert.True(result);
    }

    [Fact]
    public void NewFill_CanAdoptFreshPositionWithoutWaitingForLoss()
    {
        var result = HyperliquidCopyAdoptionPolicy.ShouldAdopt(
            alreadyCopied: false,
            alreadyBelowMinimum: false,
            existingMissing: true,
            existingClosed: false,
            isInitialSync: false,
            copyActiveOnEnable: true,
            adoptActiveOnlyWhenNegative: true,
            unrealizedPnlUsd: 5m,
            hasNewFill: true);

        Assert.True(result);
    }

    [Fact]
    public void BelowMinimumPosition_IsNotOpenedAfterOpportunityTurnsProfitable()
    {
        var result = HyperliquidCopyAdoptionPolicy.ShouldAdopt(
            alreadyCopied: false,
            alreadyBelowMinimum: true,
            existingMissing: false,
            existingClosed: false,
            isInitialSync: false,
            copyActiveOnEnable: true,
            adoptActiveOnlyWhenNegative: true,
            unrealizedPnlUsd: 5m,
            hasNewFill: false);

        Assert.False(result);
    }
}
