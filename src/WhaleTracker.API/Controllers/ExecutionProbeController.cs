using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhaleTracker.Core.Interfaces;
using WhaleTracker.Core.Models;

namespace WhaleTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/execution-probe")]
public class ExecutionProbeController : ControllerBase
{
    private readonly IOkxService _okxService;
    private readonly ILiveEventPublisher _liveEvents;

    public ExecutionProbeController(
        IOkxService okxService,
        ILiveEventPublisher liveEvents)
    {
        _okxService = okxService;
        _liveEvents = liveEvents;
    }

    [HttpPost("okx-market-order")]
    public async Task<IActionResult> PlaceOkxMarketOrder(
        [FromBody] OkxExecutionProbeRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.Symbol) ||
            request.Size <= 0)
        {
            return BadRequest(new { error = "Symbol and positive size are required." });
        }

        var symbol = request.Symbol.Trim().ToUpperInvariant();
        var side = string.IsNullOrWhiteSpace(request.Side) ? "buy" : request.Side.Trim().ToLowerInvariant();
        var posSide = string.IsNullOrWhiteSpace(request.PosSide) ? "long" : request.PosSide.Trim().ToLowerInvariant();

        var result = await _okxService.PlaceMarketOrderAsync(
            symbol,
            side,
            posSide,
            request.Size,
            request.ReduceOnly);

        await _liveEvents.PublishAsync(
            result.Success ? LiveEventTypes.TradeSubmitted : LiveEventTypes.TradeRejected,
            result.Success
                ? $"OKX execution probe accepted: {symbol} {side} {posSide} {request.Size}"
                : $"OKX execution probe rejected: {result.ErrorMessage}",
            request.WalletAddress,
            request.SourceTxHash,
            symbol,
            null,
            new
            {
                request = new
                {
                    symbol,
                    side,
                    posSide,
                    request.Size,
                    request.ReduceOnly
                },
                response = new
                {
                    result.Success,
                    result.OrderId,
                    result.Symbol,
                    result.Side,
                    result.Size,
                    result.ErrorMessage
                },
                mode = "live-execution-probe"
            },
            result.Success ? "success" : "danger",
            cancellationToken);

        return Ok(new
        {
            request = new
            {
                symbol,
                side,
                posSide,
                request.Size,
                request.ReduceOnly,
                request.WalletAddress,
                request.SourceTxHash
            },
            result = new
            {
                result.Success,
                result.OrderId,
                result.Symbol,
                result.Side,
                result.Size,
                result.ErrorMessage
            }
        });
    }
}

public sealed class OkxExecutionProbeRequest
{
    public string Symbol { get; set; } = "ETH";
    public string Side { get; set; } = "buy";
    public string PosSide { get; set; } = "long";
    public decimal Size { get; set; } = 0.01m;
    public bool ReduceOnly { get; set; }
    public string WalletAddress { get; set; } = string.Empty;
    public string SourceTxHash { get; set; } = string.Empty;
}
