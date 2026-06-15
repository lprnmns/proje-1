using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.Infrastructure.Services;

public sealed class HyperliquidConsensusExecutionWorker : BackgroundService
{
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
}
