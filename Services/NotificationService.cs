using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Interfaces.Services;
using Social.Core.Mapping;
using Social.Core.Models.Database;
using Social.Core.Models.Response;

namespace Social.Services;

public class NotificationService(INotificationRepository notificationRepository) : INotificationService
{
    public async Task<List<Notification>> GetNotificationsAsync(int page)
    {
        var result = await notificationRepository.GetNotifications(page * INotificationService.PageSize, INotificationService.PageSize);
        return result is { IsSuccess: true, Data: not null }
            ? result.Data.Select(r => r.ToNotification()).ToList()
            : new List<Notification>();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var result = await notificationRepository.GetUnreadCount();
        return result.IsSuccess ? result.Data : 0;
    }

    public Task MarkAllReadAsync() => notificationRepository.MarkAllRead();
}


public class MessageService(IMessageRepository messageRepository, IAuthService authService) : IMessageService
{
    private const int HistorySize = 50;
    /// <summary>Gültigkeit der signierten Bild-URLs; der Cache erneuert sie 5 Minuten vorher.</summary>
    private const int SignedUrlSeconds = 60 * 60;

    private readonly Dictionary<string, (string Url, DateTime ExpiresUtc)> _signedUrls = new();

    private string? CurrentUserId => authService.CurrentUser?.Id;

    public async Task<List<Conversation>> GetConversationsAsync()
    {
        var result = await messageRepository.GetConversations();
        return result is { IsSuccess: true, Data: not null }
            ? result.Data.Select(r => r.ToConversation(CurrentUserId)).ToList()
            : new List<Conversation>();
    }

    public async Task<long?> OpenConversationAsync(User other)
    {
        var result = await messageRepository.GetOrCreateConversation(other.Id);
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<ChatInfo?> GetChatAsync(long conversationId)
    {
        if (CurrentUserId is not { } me) return null;

        var infoTask = messageRepository.GetConversation(conversationId);
        var membersTask = messageRepository.GetMembers(conversationId);
        await Task.WhenAll(infoTask, membersTask);

        if (infoTask.Result is not { IsSuccess: true, Data: { } info }) return null;
        var members = membersTask.Result is { IsSuccess: true, Data: not null } ? membersTask.Result.Data : new List<User>();

        return new ChatInfo
        {
            Id = info.Id,
            IsGroup = info.IsGroup,
            Title = info.Title,
            Members = members,
            Partner = info.IsGroup ? null : members.FirstOrDefault(m => m.Id != me)
        };
    }

    public async Task<List<ChatMessage>> GetLatestMessagesAsync(long conversationId)
    {
        var result = await messageRepository.GetLatestMessages(conversationId, HistorySize);
        return await ToMessagesAsync(result.IsSuccess ? result.Data : null);
    }

    public async Task<List<ChatMessage>> GetChangesSinceAsync(long conversationId, DateTime sinceUtc)
    {
        var result = await messageRepository.GetChangesSince(conversationId, sinceUtc);
        return await ToMessagesAsync(result.IsSuccess ? result.Data : null);
    }

    public async Task<IResponse<ChatMessage>> SendAsync(long conversationId, string content, byte[]? image = null)
    {
        if (CurrentUserId is not { } me) return Response<ChatMessage>.Failure("Please sign in again.");

        content = content.Trim();
        if (content.Length > IMessageService.MaxMessageLength)
            return Response<ChatMessage>.Failure($"Messages can have up to {IMessageService.MaxMessageLength} characters.");
        if (content.Length == 0 && image is null)
            return Response<ChatMessage>.Failure("Write a message or add a photo.");

        string? imagePath = null;
        if (image is not null)
        {
            var upload = await messageRepository.UploadChatImage(conversationId, me, image, "image/jpeg");
            if (upload is not { IsSuccess: true, Data: not null })
                return Response<ChatMessage>.Failure("The photo could not be uploaded.");
            imagePath = upload.Data;
        }

        var result = await messageRepository.SendMessage(conversationId, me, content, imagePath);
        if (result is not { IsSuccess: true, Data: not null })
        {
            if (imagePath is not null) await messageRepository.DeleteChatImages(new[] { imagePath });
            return Response<ChatMessage>.Failure("Your message could not be sent.");
        }

        var message = (await ToMessagesAsync(new List<MessageRecord> { result.Data }))[0];
        // Postgrest befüllt Spalten mit ignoreOnInsert im Insert-Ergebnis nicht immer – dann gilt "jetzt"
        if (result.Data.CreatedAt == default) message.CreatedAt = DateTime.Now;
        return Response<ChatMessage>.Success(message);
    }

    public async Task<IResponse> EditAsync(ChatMessage message, string content)
    {
        if (!message.IsMine || message.IsDeleted) return Response.Failure("You can only edit your own messages.", null);

        content = content.Trim();
        if (content.Length > IMessageService.MaxMessageLength)
            return Response.Failure($"Messages can have up to {IMessageService.MaxMessageLength} characters.", null);
        if (content.Length == 0 && message.ImagePath is null)
            return Response.Failure("A message can't be empty.", null);
        if (content == message.Content) return Response.Success();

        var result = await messageRepository.EditMessage(message.Id, content);
        if (!result.IsSuccess) return Response.Failure("Your changes could not be saved.", result.Exception);

        message.Content = content;
        message.EditedAt = DateTime.Now;
        return Response.Success();
    }

    public async Task<IResponse> DeleteAsync(ChatMessage message)
    {
        if (!message.IsMine || message.IsDeleted) return Response.Failure("You can only delete your own messages.", null);

        var result = await messageRepository.DeleteMessage(message.Id);
        if (!result.IsSuccess) return Response.Failure("The message could not be deleted.", result.Exception);

        // Datei erst nach dem Löschen der Nachricht entfernen (best effort)
        if (result.Data is { } path)
        {
            await messageRepository.DeleteChatImages(new[] { path });
            _signedUrls.Remove(path);
        }

        message.Content = "";
        message.ImagePath = null;
        message.ImageUrl = null;
        message.IsDeleted = true;
        return Response.Success();
    }

    public async Task<IResponse<long>> CreateGroupAsync(string title, IReadOnlyCollection<User> members)
    {
        title = title.Trim();
        if (title.Length is 0 or > IMessageService.MaxGroupTitleLength)
            return Response<long>.Failure($"Group names can have 1 to {IMessageService.MaxGroupTitleLength} characters.");

        var ids = members.Select(m => m.Id).Where(id => id != CurrentUserId).Distinct().ToList();
        if (ids.Count < 2) return Response<long>.Failure("Add at least two people.");
        if (ids.Count >= IMessageService.MaxGroupMembers)
            return Response<long>.Failure($"Groups can have up to {IMessageService.MaxGroupMembers} members.");

        var result = await messageRepository.CreateGroup(title, ids);
        return result.IsSuccess ? result : Response<long>.Failure("The group could not be created.");
    }

    public async Task<IResponse> AddMembersAsync(long conversationId, IReadOnlyCollection<User> members)
    {
        var ids = members.Select(m => m.Id).Distinct().ToList();
        if (ids.Count == 0) return Response.Success();
        var result = await messageRepository.AddMembers(conversationId, ids);
        return result.IsSuccess
            ? result
            : Response.Failure($"People could not be added (max. {IMessageService.MaxGroupMembers} members).", result.Exception);
    }

    public async Task<IResponse> RenameGroupAsync(long conversationId, string title)
    {
        title = title.Trim();
        if (title.Length is 0 or > IMessageService.MaxGroupTitleLength)
            return Response.Failure($"Group names can have 1 to {IMessageService.MaxGroupTitleLength} characters.", null);
        var result = await messageRepository.RenameGroup(conversationId, title);
        return result.IsSuccess ? result : Response.Failure("The group could not be renamed.", result.Exception);
    }

    public async Task<IResponse> LeaveGroupAsync(long conversationId)
    {
        var result = await messageRepository.LeaveGroup(conversationId);
        return result.IsSuccess ? result : Response.Failure("You could not leave the group.", result.Exception);
    }

    public Task MarkReadAsync(long conversationId) => messageRepository.MarkRead(conversationId);

    public async Task<int> GetUnreadCountAsync()
    {
        var result = await messageRepository.GetUnreadCount();
        return result.IsSuccess ? result.Data : 0;
    }

    private async Task<List<ChatMessage>> ToMessagesAsync(List<MessageRecord>? records)
    {
        var messages = records?.Select(r => r.ToChatMessage(CurrentUserId)).ToList() ?? new List<ChatMessage>();
        await AttachImageUrlsAsync(messages);
        return messages;
    }

    /// <summary>Bilder liegen im privaten Bucket – signierte URLs gebündelt holen und zwischenspeichern.</summary>
    private async Task AttachImageUrlsAsync(List<ChatMessage> messages)
    {
        var now = DateTime.UtcNow;
        var missing = messages
            .Where(m => m.ImagePath is not null)
            .Select(m => m.ImagePath!)
            .Where(p => !_signedUrls.TryGetValue(p, out var cached) || cached.ExpiresUtc <= now)
            .Distinct()
            .ToList();

        if (missing.Count > 0)
        {
            var signed = await messageRepository.SignChatImages(missing, SignedUrlSeconds);
            if (signed is { IsSuccess: true, Data: not null })
            {
                var expires = now.AddSeconds(SignedUrlSeconds - 300);
                foreach (var (path, url) in signed.Data)
                    _signedUrls[path] = (url, expires);
            }
        }

        foreach (var message in messages)
        {
            if (message.ImagePath is { } path && _signedUrls.TryGetValue(path, out var entry))
                message.ImageUrl = entry.Url;
        }
    }
}
