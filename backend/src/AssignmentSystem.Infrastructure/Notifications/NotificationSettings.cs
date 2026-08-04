using AssignmentSystem.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Infrastructure.Notifications;

/// <summary>
/// Adapts <see cref="EmailOptions"/> to the narrow <see cref="INotificationSettings"/> port
/// the Application layer sees, so composing a message needs no configuration plumbing.
/// </summary>
internal sealed class NotificationSettings : INotificationSettings
{
    private readonly EmailOptions _options;

    public NotificationSettings(IOptions<EmailOptions> options) => _options = options.Value;

    public string AppBaseUrl => _options.AppBaseUrl;

    public int MaxDeliveryAttempts => _options.MaxDeliveryAttempts;
}
