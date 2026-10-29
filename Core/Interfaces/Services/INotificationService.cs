using Social.Core.Models.Database;

namespace Social.Core.Interfaces.Services;

public interface INotificationService
{
    Task<List<Notification>> GetNotificationsAsync();
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(int notificationId);    
}