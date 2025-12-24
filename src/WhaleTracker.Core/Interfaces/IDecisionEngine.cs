using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

/// <summary>
/// AI Karar Motoru
/// Balina işlemini analiz edip sinyal üretir
/// </summary>
public interface IDecisionEngine
{
    /// <summary>
    /// Balina işlemini analiz eder ve karar üretir
    /// </summary>
    /// <param name="whaleStats">Balinanın portföyü</param>
    /// <param name="userStats">Kullanıcının durumu</param>
    /// <param name="transaction">Balinanın yaptığı işlem</param>
    /// <returns>İşlem sinyali</returns>
    Task<TradeSignal> AnalyzeAndDecideAsync(
        WhaleStats whaleStats,
        UserStats userStats,
        TransactionEvent transaction
    );

    /// <summary>
    /// System prompt'u oluşturur
    /// </summary>
    /// <returns>System prompt metni</returns>
    string BuildSystemPrompt();

    /// <summary>
    /// User prompt'u oluşturur (JSON formatında)
    /// </summary>
    Task<string> BuildUserPromptAsync(
        WhaleStats whaleStats,
        UserStats userStats,
        TransactionEvent transaction
    );
}
