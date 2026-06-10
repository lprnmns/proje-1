using Microsoft.AspNetCore.Mvc;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.API.Controllers;

[ApiController]
[Route("api/historical-scans")]
public class HistoricalScansController : ControllerBase
{
    private readonly IHistoricalSwapScanner _scanner;

    public HistoricalScansController(IHistoricalSwapScanner scanner)
    {
        _scanner = scanner;
    }

    [HttpPost("uniswap-v3")]
    public async Task<IActionResult> ScanUniswapV3([FromBody] InsiderDetectionRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request body is required." });
        }

        if (request.PreCrashStartUtc == default ||
            request.PreCrashEndUtc == default ||
            request.DipBuyStartUtc == default ||
            request.DipBuyEndUtc == default)
        {
            return BadRequest(new { error = "All scan windows are required." });
        }

        var result = await _scanner.ScanUniswapV3Async(request, cancellationToken);
        return Ok(result);
    }
}
