using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests;

public class NotificationTests
{
    [Fact]
    public void NotificationTransitionsFromPendingToSent()
    {
        var notification = Notification.CreateSms(Guid.NewGuid(), "123456", "message", default);
        Assert.Equal(NotificationStatus.Pending, notification.Status);
        Assert.Null(notification.SentAtUtc);

        notification.MarkSent();

        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.NotNull(notification.SentAtUtc);
        Assert.Equal(NotificationChannel.Sms, notification.Channel);
    }

    [Fact]
    public void FailedNotificationRetainsError()
    {
        var notification = Notification.CreateSms(Guid.NewGuid(), "123456", "message", default);
        notification.MarkFailed("delivery failed");
        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal("delivery failed", notification.ErrorMessage);
    }
}
