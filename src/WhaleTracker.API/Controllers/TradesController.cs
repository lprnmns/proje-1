using Microsoft.AspNetCore.Mvc;
using WhaleTracker.Data.Repositories;

namespace WhaleTracker.API.Controllers;

/// <summary>
/// Trade Logs Controller
/// Geçmiş işlem kayıtları
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TradesController : ControllerBase
{
    private readonly ITradeRepository _tradeRepository;
    private readonly ILogger<TradesController> _logger;

    public TradesController(
        ITradeRepository tradeRepository,
        ILogger<TradesController> logger)
    {
        _tradeRepository = tradeRepository;
        _logger = logger;
    }

    /// <summary>
    /// Son işlemleri getir
    /// GET /api/trades?count=50
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTrades([FromQuery] int count = 50)
    {
        // TODO: Son işlemleri getir
        // var trades = await _tradeRepository.GetRecentTradeLogsAsync(count);
        // return Ok(trades);

        throw new NotImplementedException("GetTrades endpoint'ini implement et!");
    }

    /// <summary>
    /// Belirli bir coin için işlemler
    /// GET /api/trades/symbol/ETH
    /// </summary>
    [HttpGet("symbol/{symbol}")]
    public async Task<IActionResult> GetTradesBySymbol(string symbol, [FromQuery] int count = 20)
    {
        // TODO: Sembol bazlı işlemler
        // var trades = await _tradeRepository.GetTradeLogsBySymbolAsync(symbol, count);
        // return Ok(trades);

        throw new NotImplementedException("GetTradesBySymbol endpoint'ini implement et!");
    }

    /// <summary>
    /// Tarih aralığına göre işlemler
    /// GET /api/trades/range?from=2024-01-01&to=2024-12-31
    /// </summary>
    [HttpGet("range")]
    public async Task<IActionResult> GetTradesByDateRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        // TODO: Tarih aralığı ile işlemler
        // var trades = await _tradeRepository.GetTradeLogsByDateRangeAsync(from, to);
        // return Ok(trades);

        throw new NotImplementedException("GetTradesByDateRange endpoint'ini implement et!");
    }

    /// <summary>
    /// İşlem istatistikleri
    /// GET /api/trades/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetTradeStats()
    {
        // TODO: İstatistikler (toplam işlem, başarı oranı, vs.)
        
        throw new NotImplementedException("GetTradeStats endpoint'ini implement et!");
    }
}
