using AssignmentSystem.Domain.Notifications;
using Riok.Mapperly.Abstractions;

namespace AssignmentSystem.Application.Features.Notifications;

[Mapper]
public partial class NotificationMapper
{
    [MapProperty("Recipient.FullName", nameof(NotificationDto.RecipientName))]
    public partial NotificationDto MapToDto(Notification notification);
}
