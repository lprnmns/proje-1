using Microsoft.AspNetCore.Mvc;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Data.Repositories;

namespace WhaleTracker.API.Controllers;

/// <summary>
/// Dashboard Controller
/// Web arayüzü için veri sağlar
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IOkxService _okxService;
    private readonly ITradeRepository _tradeRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IOkxService okxService,
        ITradeRepository tradeRepository,
        ILogger<DashboardController> logger)
    {
        _okxService = okxService;
        _tradeRepository = tradeRepository;
        _logger = logger;
    }

    /// <summary>
    /// Anlık durum özeti
    /// GET /api/dashboard/status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        // TODO: Dashboard durumunu getir
        // 
        // var userStats = await _okxService.GetAccountInfoAsync();
        // var recentTrades = await _tradeRepository.GetRecentTradeLogsAsync(10);
        // 
        // return Ok(new
        // {
        //     Balance = userStats.TotalUsd,
        //     OpenPositions = userStats.ActivePositions.Count,
        //     Positions = userStats.ActivePositions,
        //     RecentTrades = recentTrades.Take(5),
        //     ServerTime = DateTime.UtcNow
        // });

        throw new NotImplementedException("GetStatus endpoint'ini implement et!");
    }

    /// <summary>
    /// PnL geçmişi
    /// GET /api/dashboard/pnl?from=2024-01-01&to=2024-12-31
    /// </summary>
    [HttpGet("pnl")]
    public async Task<IActionResult> GetPnlHistory(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        // TODO: PnL geçmişini getir
        
        throw new NotImplementedException("GetPnlHistory endpoint'ini implement et!");
    }

    /// <summary>
    /// Açık pozisyonlar
    /// GET /api/dashboard/positions
    /// </summary>
    [HttpGet("positions")]
    public async Task<IActionResult> GetPositions()
    {
        // TODO: Açık pozisyonları getir
        // var positions = await _okxService.GetAllPositionsAsync();
        // return Ok(positions);

        throw new NotImplementedException("GetPositions endpoint'ini implement et!");
    }
}
