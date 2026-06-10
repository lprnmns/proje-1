using Microsoft.EntityFrameworkCore;
using WhaleTracker.Data.Entities;

namespace WhaleTracker.Data.Repositories;

/// <summary>
/// Veritabanı işlemleri implementasyonu
/// SEN BURADA KOD YAZACAKSIN
/// </summary>
public class TradeRepository : ITradeRepository
{
    private readonly WhaleTrackerDbContext _context;
    private const int MaxQueryCount = 1000;

    public TradeRepository(WhaleTrackerDbContext context)
    {
        _context = context;
    }

    // ========================
    // TRADE LOG İŞLEMLERİ
    // ========================

    public async Task<TradeLogEntity> SaveTradeLogAsync(TradeLogEntity log)
    {
        if (log.CreatedAt == default)
        {
            log.CreatedAt = DateTime.UtcNow;
        }

        _context.TradeLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<List<TradeLogEntity>> GetRecentTradeLogsAsync(int count = 50)
    {
        count = NormalizeCount(count);

        return await _context.TradeLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<TradeLogEntity>> GetTradeLogsBySymbolAsync(string symbol, int count = 20)
    {
        count = NormalizeCount(count);
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        return await _context.TradeLogs
            .AsNoTracking()
            .Where(x => x.Symbol.ToUpper() == normalizedSymbol)
            .OrderByDescending(x => x.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<TradeLogEntity>> GetTradeLogsByDateRangeAsync(DateTime from, DateTime to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        return await _context.TradeLogs
            .AsNoTracking()
            .Where(x => x.CreatedAt >= from.ToUniversalTime() && x.CreatedAt <= to.ToUniversalTime())
            .OrderByDescending(x => x.CreatedAt)
            .Take(MaxQueryCount)
            .ToListAsync();
    }

    // ========================
    // PNL İŞLEMLERİ
    // ========================

    public async Task SavePnlSnapshotAsync(PnlHistoryEntity pnl)
    {
        if (pnl.RecordedAt == default)
        {
            pnl.RecordedAt = DateTime.UtcNow;
        }

        _context.PnlHistory.Add(pnl);
        await _context.SaveChangesAsync();
    }

    public async Task<List<PnlHistoryEntity>> GetPnlHistoryAsync(DateTime from, DateTime to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        return await _context.PnlHistory
            .AsNoTracking()
            .Where(x => x.RecordedAt >= from.ToUniversalTime() && x.RecordedAt <= to.ToUniversalTime())
            .OrderBy(x => x.RecordedAt)
            .Take(MaxQueryCount)
            .ToListAsync();
    }

    // ========================
    // PROCESSED TX İŞLEMLERİ
    // ========================

    public async Task<bool> IsTransactionProcessedAsync(string txHash)
    {
        if (string.IsNullOrWhiteSpace(txHash))
        {
            return false;
        }

        return await _context.ProcessedTransactions
            .AsNoTracking()
            .AnyAsync(x => x.TxHash == txHash);
    }

    public async Task MarkTransactionProcessedAsync(string txHash, bool wasTraded)
    {
        if (string.IsNullOrWhiteSpace(txHash))
        {
            return;
        }

        var existing = await _context.ProcessedTransactions.FindAsync(txHash);
        if (existing == null)
        {
            _context.ProcessedTransactions.Add(new ProcessedTransactionEntity
            {
                TxHash = txHash,
                ProcessedAt = DateTime.UtcNow,
                WasTraded = wasTraded
            });
        }
        else
        {
            existing.ProcessedAt = DateTime.UtcNow;
            existing.WasTraded = wasTraded;
        }

        await _context.SaveChangesAsync();
    }

    // ========================
    // POZİSYON SNAPSHOT
    // ========================

    public async Task SavePositionSnapshotAsync(PositionSnapshotEntity snapshot)
    {
        if (snapshot.SnapshotTime == default)
        {
            snapshot.SnapshotTime = DateTime.UtcNow;
        }

        _context.PositionSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();
    }

    public async Task<List<PositionSnapshotEntity>> GetRecentPositionSnapshotsAsync(int count = 100)
    {
        count = NormalizeCount(count);

        return await _context.PositionSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.SnapshotTime)
            .Take(count)
            .ToListAsync();
    }

    private static int NormalizeCount(int count)
    {
        return Math.Clamp(count, 1, MaxQueryCount);
    }
}
