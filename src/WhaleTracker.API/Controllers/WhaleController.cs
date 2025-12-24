using Microsoft.AspNetCore.Mvc;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.API.Controllers;

/// <summary>
/// Whale Controller
/// Balina cüzdanı takibi
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WhaleController : ControllerBase
{
    private readonly IZerionService _zerionService;
    private readonly IWhaleTrackerService _whaleTrackerService;
    private readonly ILogger<WhaleController> _logger;
    private readonly string _whaleAddress;

    public WhaleController(
        IZerionService zerionService,
        IWhaleTrackerService whaleTrackerService,
        IConfiguration configuration,
        ILogger<WhaleController> logger)
    {
        _zerionService = zerionService;
        _whaleTrackerService = whaleTrackerService;
        _logger = logger;
        _whaleAddress = configuration["Zerion:WhaleAddress"] ?? "";
    }

    /// <summary>
    /// Balinanın portföyünü getir
    /// GET /api/whale/portfolio
    /// </summary>
    [HttpGet("portfolio")]
    public async Task<IActionResult> GetWhalePortfolio()
    {
        // TODO: Balina portföyünü getir
        // var portfolio = await _zerionService.GetWalletPortfolioAsync(_whaleAddress);
        // return Ok(portfolio);

        throw new NotImplementedException("GetWhalePortfolio endpoint'ini implement et!");
    }

    /// <summary>
    /// Balinanın son işlemleri
    /// GET /api/whale/transactions?limit=20
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetWhaleTransactions([FromQuery] int limit = 20)
    {
        // TODO: Balina işlemlerini getir
        // var transactions = await _zerionService.GetRecentTransactionsAsync(_whaleAddress, limit);
        // return Ok(transactions);

        throw new NotImplementedException("GetWhaleTransactions endpoint'ini implement et!");
    }

    /// <summary>
    /// Manuel işlem tetikle (test için)
    /// POST /api/whale/process
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessTransaction([FromBody] TransactionEvent transaction)
    {
        // TODO: Manuel olarak bir işlemi işle (test için)
        // var signal = await _whaleTrackerService.ProcessTransactionAsync(transaction);
        // return Ok(signal);

        throw new NotImplementedException("ProcessTransaction endpoint'ini implement et!");
    }

    /// <summary>
    /// Servisi manuel tetikle
    /// POST /api/whale/scan
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> TriggerScan()
    {
        // TODO: Manuel tarama tetikle
        // await _whaleTrackerService.ScanAndProcessAsync();
        // return Ok(new { message = "Scan completed" });

        throw new NotImplementedException("TriggerScan endpoint'ini implement et!");
    }
}
