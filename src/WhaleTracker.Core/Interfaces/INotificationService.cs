namespace WhaleTracker.Core.Interfaces;

public interface INotificationService
{
    Task SendAsync(string title, string message, CancellationToken cancellationToken = default);
}
