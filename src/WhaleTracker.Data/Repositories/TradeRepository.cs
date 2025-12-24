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

    public TradeRepository(WhaleTrackerDbContext context)
    {
        _context = context;
    }

    // ========================
    // TRADE LOG İŞLEMLERİ
    // ========================

    public async Task<TradeLogEntity> SaveTradeLogAsync(TradeLogEntity log)
    {
        // TODO: Burada işlem logu kaydetme kodunu yaz
        // Örnek:
        // _context.TradeLogs.Add(log);
        // await _context.SaveChangesAsync();
        // return log;

        throw new NotImplementedException("SaveTradeLogAsync metodunu implement et!");
    }

    public async Task<List<TradeLogEntity>> GetRecentTradeLogsAsync(int count = 50)
    {
        // TODO: Son N işlem logunu getir
        // Örnek:
        // return await _context.TradeLogs
        //     .OrderByDescending(x => x.CreatedAt)
        //     .Take(count)
        //     .ToListAsync();

        throw new NotImplementedException("GetRecentTradeLogsAsync metodunu implement et!");
    }

    public async Task<List<TradeLogEntity>> GetTradeLogsBySymbolAsync(string symbol, int count = 20)
    {
        // TODO: Belirli sembol için işlem loglarını getir
        
        throw new NotImplementedException("GetTradeLogsBySymbolAsync metodunu implement et!");
    }

    public async Task<List<TradeLogEntity>> GetTradeLogsByDateRangeAsync(DateTime from, DateTime to)
    {
        // TODO: Tarih aralığına göre logları getir

        throw new NotImplementedException("GetTradeLogsByDateRangeAsync metodunu implement et!");
    }

    // ========================
    // PNL İŞLEMLERİ
    // ========================

    public async Task SavePnlSnapshotAsync(PnlHistoryEntity pnl)
    {
        // TODO: PnL snapshot kaydet

        throw new NotImplementedException("SavePnlSnapshotAsync metodunu implement et!");
    }

    public async Task<List<PnlHistoryEntity>> GetPnlHistoryAsync(DateTime from, DateTime to)
    {
        // TODO: PnL geçmişini getir

        throw new NotImplementedException("GetPnlHistoryAsync metodunu implement et!");
    }

    // ========================
    // PROCESSED TX İŞLEMLERİ
    // ========================

    public async Task<bool> IsTransactionProcessedAsync(string txHash)
    {
        // TODO: Transaction daha önce işlendi mi kontrol et
        // Örnek:
        // return await _context.ProcessedTransactions
        //     .AnyAsync(x => x.TxHash == txHash);

        throw new NotImplementedException("IsTransactionProcessedAsync metodunu implement et!");
    }

    public async Task MarkTransactionProcessedAsync(string txHash, bool wasTraded)
    {
        // TODO: Transaction'ı işlendi olarak kaydet

        throw new NotImplementedException("MarkTransactionProcessedAsync metodunu implement et!");
    }

    // ========================
    // POZİSYON SNAPSHOT
    // ========================

    public async Task SavePositionSnapshotAsync(PositionSnapshotEntity snapshot)
    {
        // TODO: Pozisyon snapshot kaydet

        throw new NotImplementedException("SavePositionSnapshotAsync metodunu implement et!");
    }

    public async Task<List<PositionSnapshotEntity>> GetRecentPositionSnapshotsAsync(int count = 100)
    {
        // TODO: Son pozisyon snapshot'larını getir

        throw new NotImplementedException("GetRecentPositionSnapshotsAsync metodunu implement et!");
    }
}
