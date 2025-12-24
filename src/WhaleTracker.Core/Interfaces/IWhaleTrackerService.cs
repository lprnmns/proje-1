using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

/// <summary>
/// Ana orkestrasyon servisi
/// Tüm akışı yönetir
/// </summary>
public interface IWhaleTrackerService
{
    /// <summary>
    /// Balina cüzdanını tarar ve yeni işlem varsa işler
    /// Background service tarafından periyodik çağrılır
    /// </summary>
    Task ScanAndProcessAsync();

    /// <summary>
    /// Tek bir işlemi manuel olarak işler
    /// Test ve debug için
    /// </summary>
    Task<TradeSignal> ProcessTransactionAsync(TransactionEvent transaction);

    /// <summary>
    /// Servisi başlatır
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Servisi durdurur
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
