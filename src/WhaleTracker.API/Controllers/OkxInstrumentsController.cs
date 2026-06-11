using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleTracker.Core.Interfaces;

namespace WhaleTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/okx-instruments")]
public class OkxInstrumentsController : ControllerBase
{
    private readonly IOkxService _okxService;

    public OkxInstrumentsController(IOkxService okxService)
    {
        _okxService = okxService;
    }

    [HttpGet("symbols")]
    public async Task<IActionResult> Symbols([FromQuery] bool forceRefresh = false)
    {
        var symbols = await _okxService.GetSupportedSymbolsAsync(forceRefresh);
        return Ok(new
        {
            count = symbols.Count,
            symbols = symbols.OrderBy(x => x).ToArray()
        });
    }

    [HttpGet("symbols/{symbol}")]
    public async Task<IActionResult> IsSupported(string symbol, [FromQuery] bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest(new { error = "Symbol is required." });
        }

        var normalized = symbol.Trim().ToUpperInvariant();
        var supported = await _okxService.IsSymbolSupportedAsync(normalized, forceRefresh);
        return Ok(new
        {
            symbol = normalized,
            supported
        });
    }
}
