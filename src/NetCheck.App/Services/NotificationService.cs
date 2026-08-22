namespace NetCheck.App.Services;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<AppNotification>? NotificationRaised;

    public void Show(string title, string message, NotificationKind kind) =>
        NotificationRaised?.Invoke(this, new AppNotification(
            title,
            message,
            kind,
            DateTimeOffset.UtcNow));
}
