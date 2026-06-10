using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhaleTracker.Data;
using WhaleTracker.Data.Entities;

namespace WhaleTracker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/tracked-wallets")]
public class TrackedWalletsController : ControllerBase
{
    private readonly WhaleTrackerDbContext _db;

    public TrackedWalletsController(WhaleTrackerDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.TrackedWallets.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var wallets = await query
            .OrderByDescending(x => x.ConfidenceScore)
            .ThenByDescending(x => x.EstimatedProfitUsd)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.WalletAddress,
                x.Label,
                x.Source,
                x.Chain,
                x.IsActive,
                x.ConfidenceScore,
                x.EstimatedProfitUsd,
                x.AssetSymbol,
                x.HistoricalScanId,
                x.InsiderCandidateId,
                x.Notes,
                x.LastCheckedAt,
                x.LastSeenTxHash,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(wallets);
    }

    [HttpPost]
    public async Task<IActionResult> AddManual(
        [FromBody] UpsertTrackedWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !IsValidEvmAddress(request.WalletAddress))
        {
            return BadRequest(new { error = "Valid EVM wallet address is required." });
        }

        var wallet = await UpsertWalletAsync(new TrackedWalletEntity
        {
            WalletAddress = NormalizeAddress(request.WalletAddress),
            Label = request.Label?.Trim() ?? string.Empty,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim(),
            Chain = string.IsNullOrWhiteSpace(request.Chain) ? "ethereum" : request.Chain.Trim(),
            IsActive = request.IsActive,
            ConfidenceScore = Math.Clamp(request.ConfidenceScore, 0, 100),
            EstimatedProfitUsd = Math.Max(0, request.EstimatedProfitUsd),
            AssetSymbol = request.AssetSymbol?.Trim().ToUpperInvariant() ?? string.Empty,
            Notes = request.Notes?.Trim() ?? string.Empty
        }, cancellationToken);

        return Ok(wallet);
    }

    [HttpPost("from-candidate/{candidateId:long}")]
    public async Task<IActionResult> AddFromCandidate(
        long candidateId,
        [FromBody] PromoteCandidateRequest? request,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _db.InsiderCandidates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == candidateId, cancellationToken);

        if (candidate == null)
        {
            return NotFound(new { error = "Candidate not found." });
        }

        var wallet = await UpsertWalletAsync(new TrackedWalletEntity
        {
            WalletAddress = NormalizeAddress(candidate.WalletAddress),
            Label = string.IsNullOrWhiteSpace(request?.Label)
                ? $"insider-{candidate.AssetSymbol}-{candidate.Id}"
                : request.Label.Trim(),
            Source = "historical_scan",
            Chain = string.IsNullOrWhiteSpace(request?.Chain) ? "ethereum" : request.Chain.Trim(),
            IsActive = request?.IsActive ?? true,
            ConfidenceScore = candidate.InsiderScore,
            EstimatedProfitUsd = candidate.EstimatedProfitUsd,
            AssetSymbol = candidate.AssetSymbol,
            HistoricalScanId = candidate.HistoricalScanId,
            InsiderCandidateId = candidate.Id,
            Notes = request?.Notes?.Trim() ?? "Promoted from historical insider candidate."
        }, cancellationToken);

        return Ok(wallet);
    }

    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateTrackedWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        var wallet = await _db.TrackedWallets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (wallet == null)
        {
            return NotFound(new { error = "Tracked wallet not found." });
        }

        if (request.Label != null)
        {
            wallet.Label = request.Label.Trim();
        }

        if (request.Notes != null)
        {
            wallet.Notes = request.Notes.Trim();
        }

        if (request.IsActive.HasValue)
        {
            wallet.IsActive = request.IsActive.Value;
        }

        wallet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(wallet);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        var wallet = await _db.TrackedWallets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (wallet == null)
        {
            return NotFound(new { error = "Tracked wallet not found." });
        }

        _db.TrackedWallets.Remove(wallet);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<TrackedWalletEntity> UpsertWalletAsync(
        TrackedWalletEntity incoming,
        CancellationToken cancellationToken)
    {
        var existing = await _db.TrackedWallets
            .FirstOrDefaultAsync(x => x.WalletAddress == incoming.WalletAddress, cancellationToken);

        if (existing == null)
        {
            incoming.CreatedAt = DateTime.UtcNow;
            incoming.UpdatedAt = incoming.CreatedAt;
            _db.TrackedWallets.Add(incoming);
            await _db.SaveChangesAsync(cancellationToken);
            return incoming;
        }

        existing.Label = string.IsNullOrWhiteSpace(incoming.Label) ? existing.Label : incoming.Label;
        existing.Source = incoming.Source;
        existing.Chain = incoming.Chain;
        existing.IsActive = incoming.IsActive;
        existing.ConfidenceScore = Math.Max(existing.ConfidenceScore, incoming.ConfidenceScore);
        existing.EstimatedProfitUsd = Math.Max(existing.EstimatedProfitUsd, incoming.EstimatedProfitUsd);
        existing.AssetSymbol = string.IsNullOrWhiteSpace(incoming.AssetSymbol) ? existing.AssetSymbol : incoming.AssetSymbol;
        existing.HistoricalScanId = incoming.HistoricalScanId ?? existing.HistoricalScanId;
        existing.InsiderCandidateId = incoming.InsiderCandidateId ?? existing.InsiderCandidateId;
        existing.Notes = string.IsNullOrWhiteSpace(incoming.Notes) ? existing.Notes : incoming.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private static bool IsValidEvmAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var normalized = address.Trim();
        return normalized.Length == 42 &&
               normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
               normalized.Skip(2).All(Uri.IsHexDigit);
    }

    private static string NormalizeAddress(string address)
    {
        return address.Trim().ToLowerInvariant();
    }
}

public class UpsertTrackedWalletRequest
{
    public string WalletAddress { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Source { get; set; }
    public string? Chain { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal ConfidenceScore { get; set; }
    public decimal EstimatedProfitUsd { get; set; }
    public string? AssetSymbol { get; set; }
    public string? Notes { get; set; }
}

public class PromoteCandidateRequest
{
    public string? Label { get; set; }
    public string? Chain { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public class UpdateTrackedWalletRequest
{
    public string? Label { get; set; }
    public string? Notes { get; set; }
    public bool? IsActive { get; set; }
}
