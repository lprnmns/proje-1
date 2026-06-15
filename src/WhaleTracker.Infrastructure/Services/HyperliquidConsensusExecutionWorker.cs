using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

public sealed class HyperliquidConsensusExecutionWorker : BackgroundService
{
    private static readonly Dictionary<string, bool> ThresholdStates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DateTime> LastNotificationAt = new(StringComparer.OrdinalIgnoreCase);
    private static bool _thresholdStatesInitialized;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HyperliquidConsensusExecutionWorker> _logger;
    private readonly HyperliquidConsensusExecutionSettings _settings;

    public HyperliquidConsensusExecutionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AppSettings> options,
        ILogger<HyperliquidConsensusExecutionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = options.Value.HyperliquidConsensusExecution;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Hyperliquid consensus execution worker started. Enabled={Enabled}, TickSeconds={TickSeconds}",
            _settings.Enabled,
            _settings.TickSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_settings.Enabled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IHyperliquidConsensusExecutionService>();
                    var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var plan = await service.BuildPlanAsync(stoppingToken);
                    await NotifyThresholdCrossingsAsync(plan, notifier, stoppingToken);
                    var result = await service.ApplyPlanAsync(stoppingToken);
                    _logger.LogInformation(
                        "Consensus execution tick completed. Success={Success}, rows={Rows}",
                        result.Success,
                        result.Results.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hyperliquid consensus execution worker tick failed.");
            }

            var delaySeconds = Math.Clamp(_settings.TickSeconds, 3, 300);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
        }
    }

    private async Task NotifyThresholdCrossingsAsync(
        HyperliquidConsensusExecutionPlan plan,
        INotificationService notifier,
        CancellationToken cancellationToken)
    {
        var threshold = Math.Abs(plan.Config.Threshold);
        if (threshold <= 0)
        {
            return;
        }

        var rows = plan.Rows
            .GroupBy(x => x.Coin, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();

        if (!_thresholdStatesInitialized)
        {
            foreach (var row in rows)
            {
                ThresholdStates[row.Coin] = Math.Abs(row.DirectionScore) >= threshold;
            }

            _thresholdStatesInitialized = true;
            return;
        }

        foreach (var row in rows)
        {
            var isAbove = Math.Abs(row.DirectionScore) >= threshold;
            ThresholdStates.TryGetValue(row.Coin, out var wasAbove);
            if (isAbove == wasAbove)
            {
                continue;
            }

            ThresholdStates[row.Coin] = isAbove;
            var notificationKey = $"{row.Coin}:{(isAbove ? "crossed" : "dropped")}";
            var now = DateTime.UtcNow;
            if (LastNotificationAt.TryGetValue(notificationKey, out var last) &&
                now - last < TimeSpan.FromMinutes(15))
            {
                continue;
            }

            LastNotificationAt[notificationKey] = now;
            var direction = row.DirectionScore > 0 ? "LONG bias" : row.DirectionScore < 0 ? "SHORT bias" : "FLAT";
            var title = isAbove ? "Consensus threshold crossed" : "Consensus threshold dropped";
            var message = isAbove
                ? $"{row.Coin} threshold ustune cikti. Score {row.DirectionScore:+0.00;-0.00;0.00}, side {direction}, target {row.TargetSide}, action {row.Action}, target margin ${row.TargetMarginUsd:0.##}."
                : $"{row.Coin} threshold altina dustu. Score {row.DirectionScore:+0.00;-0.00;0.00}, onceki sinyal kapandi/zayifladi. Current action {row.Action}, reason {row.SkipReason}.";
            await notifier.SendAsync(title, message, cancellationToken);
        }
    }
}
