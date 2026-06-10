using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleTracker.Core.Interfaces;

namespace WhaleTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/wallet-activity")]
public class WalletActivityController : ControllerBase
{
    private readonly IWalletActivityService _walletActivityService;

    public WalletActivityController(IWalletActivityService walletActivityService)
    {
        _walletActivityService = walletActivityService;
    }

    [HttpGet("{address}")]
    public async Task<IActionResult> Recent(
        string address,
        [FromQuery] string fromBlock = "0x0",
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address) || !address.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Valid EVM wallet address is required." });
        }

        var movements = await _walletActivityService.GetRecentTokenMovementsAsync(address, fromBlock, limit, cancellationToken);
        return Ok(movements);
    }
}
