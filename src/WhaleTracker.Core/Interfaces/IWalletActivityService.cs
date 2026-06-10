using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

public interface IWalletActivityService
{
    Task<List<TransactionEvent>> GetRecentTokenMovementsAsync(
        string walletAddress,
        string fromBlock = "0x0",
        int limit = 20,
        CancellationToken cancellationToken = default);
}
