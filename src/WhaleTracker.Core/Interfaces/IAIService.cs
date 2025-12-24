using WhaleTracker.Core.Models;

namespace WhaleTracker.Core.Interfaces;

/// <summary>
/// AI Servisi Interface
/// Balina hareketlerini analiz edip işlem kararı verir
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Balina hareketini analiz et ve işlem kararı ver
    /// </summary>
    /// <param name="context">AI'a gönderilecek context</param>
    /// <returns>AI'ın işlem kararı</returns>
    Task<AIDecision> AnalyzeMovementAsync(AIContext context);

    /// <summary>
    /// API bağlantısını test et
    /// </summary>
    Task<(bool success, string message)> TestConnectionAsync();

    /// <summary>
    /// Basit bir soru sor (debug/test için)
    /// </summary>
    Task<string> AskAsync(string question);
}
