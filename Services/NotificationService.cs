using Social.Core.Interfaces.Services;
using Social.Core.Models.Database;
using Social.Core.Models.Enums;

namespace Social.Services;

public class NotificationService : INotificationService
{

    public NotificationService()
    {
    }

    public async Task<List<Notification>> GetNotificationsAsync()
    {
        await Task.Delay(400);
        return new List<Notification>
        {
            new Notification
            {
                Id = 1,
                User = new User { Username = "john_doe", DisplayName = "John Doe", ProfileImageUrl = "https://via.placeholder.com/40x40" },
                Message = "liked your post",
                Type = NotificationType.Like,
                CreatedAt = DateTime.Now.AddMinutes(-10)
            },
            new Notification
            {
                Id = 2,
                User = new User { Username = "sarah_wilson", DisplayName = "Sarah Wilson", ProfileImageUrl = "https://via.placeholder.com/40x40" },
                Message = "commented on your post",
                Type = NotificationType.Comment,
                CreatedAt = DateTime.Now.AddMinutes(-30),
                IsRead = true
            }
        };
    }

    public async Task<int> GetUnreadCountAsync()
    {
        await Task.Delay(200);
        return 3;
    }

    public async Task MarkAsReadAsync(int notificationId)
    {
        await Task.Delay(300);
        // Mark notification as read
    }
}