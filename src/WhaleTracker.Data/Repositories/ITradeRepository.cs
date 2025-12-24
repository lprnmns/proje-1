using WhaleTracker.Data.Entities;

namespace WhaleTracker.Data.Repositories;

/// <summary>
/// Veritabanı işlemleri için interface
/// </summary>
public interface ITradeRepository
{
    // ========================
    // TRADE LOG İŞLEMLERİ
    // ========================

    /// <summary>
    /// Yeni işlem logu kaydet
    /// </summary>
    Task<TradeLogEntity> SaveTradeLogAsync(TradeLogEntity log);

    /// <summary>
    /// Son N işlem logunu getir
    /// </summary>
    Task<List<TradeLogEntity>> GetRecentTradeLogsAsync(int count = 50);

    /// <summary>
    /// Belirli bir sembol için işlem loglarını getir
    /// </summary>
    Task<List<TradeLogEntity>> GetTradeLogsBySymbolAsync(string symbol, int count = 20);

    /// <summary>
    /// Tarih aralığına göre işlem logları
    /// </summary>
    Task<List<TradeLogEntity>> GetTradeLogsByDateRangeAsync(DateTime from, DateTime to);

    // ========================
    // PNL İŞLEMLERİ
    // ========================

    /// <summary>
    /// PnL kaydı ekle
    /// </summary>
    Task SavePnlSnapshotAsync(PnlHistoryEntity pnl);

    /// <summary>
    /// PnL geçmişini getir
    /// </summary>
    Task<List<PnlHistoryEntity>> GetPnlHistoryAsync(DateTime from, DateTime to);

    // ========================
    // PROCESSED TX İŞLEMLERİ
    // ========================

    /// <summary>
    /// Transaction işlendi mi kontrol et
    /// </summary>
    Task<bool> IsTransactionProcessedAsync(string txHash);

    /// <summary>
    /// Transaction'ı işlendi olarak işaretle
    /// </summary>
    Task MarkTransactionProcessedAsync(string txHash, bool wasTraded);

    // ========================
    // POZİSYON SNAPSHOT
    // ========================

    /// <summary>
    /// Pozisyon snapshot kaydet
    /// </summary>
    Task SavePositionSnapshotAsync(PositionSnapshotEntity snapshot);

    /// <summary>
    /// Son pozisyon snapshot'larını getir
    /// </summary>
    Task<List<PositionSnapshotEntity>> GetRecentPositionSnapshotsAsync(int count = 100);
}
