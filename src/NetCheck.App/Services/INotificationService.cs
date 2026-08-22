namespace NetCheck.App.Services;

public interface INotificationService
{
    event EventHandler<AppNotification>? NotificationRaised;

    void Show(string title, string message, NotificationKind kind);
}

public enum NotificationKind
{
    Information,
    Warning,
    Success
}

public sealed record AppNotification(
    string Title,
    string Message,
    NotificationKind Kind,
    DateTimeOffset OccurredAtUtc);
