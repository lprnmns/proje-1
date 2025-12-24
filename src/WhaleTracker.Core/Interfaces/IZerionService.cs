using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

/// <summary>
/// Zerion API ile iletişim
/// Balina cüzdanını takip eder
/// </summary>
public interface IZerionService
{
    /// <summary>
    /// Balinanın güncel portföyünü çeker
    /// </summary>
    /// <param name="walletAddress">Cüzdan adresi</param>
    /// <returns>WhaleStats objesi</returns>
    Task<WhaleStats> GetWalletPortfolioAsync(string walletAddress);

    /// <summary>
    /// Balinanın son işlemlerini çeker
    /// </summary>
    /// <param name="walletAddress">Cüzdan adresi</param>
    /// <param name="limit">Kaç işlem çekilsin</param>
    /// <returns>İşlem listesi</returns>
    Task<List<TransactionEvent>> GetRecentTransactionsAsync(string walletAddress, int limit = 20);

    /// <summary>
    /// Belirli bir işlemin detayını çeker
    /// </summary>
    /// <param name="txHash">Transaction hash</param>
    /// <returns>İşlem detayı</returns>
    Task<TransactionEvent?> GetTransactionDetailAsync(string txHash);
}
