using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using static Supabase.Postgrest.Constants;
using FileOptions = Supabase.Storage.FileOptions;

namespace Social.Infrastructure;

public class NotificationRepository(Supabase.Client client) : INotificationRepository
{
    public async Task<IResponse<List<NotificationRecord>>> GetNotifications(int offset, int limit)
    {
        try
        {
            // RLS (007): nur eigene Benachrichtigungen
            var result = await client.From<NotificationRecord>()
                .Order(x => x.CreatedAt, Ordering.Descending)
                .Range(offset, offset + limit - 1)
                .Get();
            return Response<List<NotificationRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<NotificationRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<int>> GetUnreadCount()
    {
        try
        {
            return Response<int>.Success(await client.Rpc<int>("unread_notification_count", null));
        }
        catch (Exception ex)
        {
            return Response<int>.Failure(ex);
        }
    }

    public async Task<IResponse> MarkAllRead()
    {
        try
        {
            await client.Rpc("mark_notifications_read", null);
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }
}

public class MessageRepository(Supabase.Client client) : IMessageRepository
{
    private const string ImageBucket = "chat-images";

    public async Task<IResponse<List<ConversationRecord>>> GetConversations()
    {
        try
        {
            var result = await client.From<ConversationRecord>()
                .Order(x => x.LastMessageAt, Ordering.Descending)
                .Get();
            return Response<List<ConversationRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<ConversationRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<long>> GetOrCreateConversation(string otherUserId)
    {
        try
        {
            var id = await client.Rpc<long>("get_or_create_direct_conversation", new Dictionary<string, object> { ["p_other"] = otherUserId });
            return Response<long>.Success(id);
        }
        catch (Exception ex)
        {
            return Response<long>.Failure(ex);
        }
    }

    public async Task<IResponse<ConversationInfoRecord>> GetConversation(long conversationId)
    {
        try
        {
            // RLS (016): nur Mitglieder sehen die Unterhaltung
            var result = await client.From<ConversationInfoRecord>().Where(x => x.Id == conversationId).Get();
            return result.Model is not null
                ? Response<ConversationInfoRecord>.Success(result.Model)
                : Response<ConversationInfoRecord>.Failure("Conversation not found");
        }
        catch (Exception ex)
        {
            return Response<ConversationInfoRecord>.Failure(ex);
        }
    }

    public async Task<IResponse<List<User>>> GetMembers(long conversationId)
    {
        try
        {
            var result = await client.Rpc<List<User>>("conversation_member_profiles",
                new Dictionary<string, object> { ["p_conversation_id"] = conversationId });
            return Response<List<User>>.Success(result ?? new List<User>());
        }
        catch (Exception ex)
        {
            return Response<List<User>>.Failure(ex);
        }
    }

    public async Task<IResponse<List<MessageRecord>>> GetLatestMessages(long conversationId, int limit)
    {
        try
        {
            var result = await client.From<MessageRecord>()
                .Where(x => x.ConversationId == conversationId)
                .Order(x => x.Id, Ordering.Descending)
                .Limit(limit)
                .Get();
            var messages = result.Models;
            messages.Reverse();
            return Response<List<MessageRecord>>.Success(messages);
        }
        catch (Exception ex)
        {
            return Response<List<MessageRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<List<MessageRecord>>> GetChangesSince(long conversationId, DateTime sinceUtc)
    {
        try
        {
            // ">=" statt ">": gleiche Zeitstempel nicht verpassen – doppelte filtert der Aufrufer per ID
            var result = await client.From<MessageRecord>()
                .Where(x => x.ConversationId == conversationId)
                .Filter("updated_at", Operator.GreaterThanOrEqual, sinceUtc.ToUniversalTime().ToString("o"))
                .Order(x => x.UpdatedAt, Ordering.Ascending)
                .Get();
            return Response<List<MessageRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<MessageRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<MessageRecord>> SendMessage(long conversationId, string senderId, string content, string? imagePath)
    {
        try
        {
            var result = await client.From<MessageRecord>()
                .Insert(new MessageRecord { ConversationId = conversationId, SenderId = senderId, Content = content, ImagePath = imagePath });
            return result.Model is not null ? Response<MessageRecord>.Success(result.Model) : Response<MessageRecord>.Failure("Message not sent");
        }
        catch (Exception ex)
        {
            return Response<MessageRecord>.Failure(ex);
        }
    }

    public async Task<IResponse> EditMessage(long messageId, string content)
    {
        try
        {
            await client.Rpc("edit_message", new Dictionary<string, object> { ["p_message_id"] = messageId, ["p_content"] = content });
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<string?>> DeleteMessage(long messageId)
    {
        try
        {
            var path = await client.Rpc<string?>("delete_message", new Dictionary<string, object> { ["p_message_id"] = messageId });
            return Response<string?>.Success(string.IsNullOrEmpty(path) ? null : path);
        }
        catch (Exception ex)
        {
            return Response<string?>.Failure(ex);
        }
    }

    public async Task<IResponse<string>> UploadChatImage(long conversationId, string userId, byte[] data, string contentType)
    {
        try
        {
            // Pfad <conversation_id>/<user_id>/… – geprüft von den Policies in 020
            var extension = contentType switch { "image/png" => "png", "image/webp" => "webp", _ => "jpg" };
            var path = $"{conversationId}/{userId}/{Guid.NewGuid():N}.{extension}";
            await client.Storage.From(ImageBucket).Upload(data, path, new FileOptions { ContentType = contentType });
            return Response<string>.Success(path);
        }
        catch (Exception ex)
        {
            return Response<string>.Failure(ex);
        }
    }

    public async Task<IResponse> DeleteChatImages(IEnumerable<string> paths)
    {
        var list = paths.Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (list.Count == 0) return Response.Success();
        try
        {
            await client.Storage.From(ImageBucket).Remove(list);
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<Dictionary<string, string>>> SignChatImages(IReadOnlyCollection<string> paths, int expiresInSeconds)
    {
        if (paths.Count == 0) return Response<Dictionary<string, string>>.Success(new Dictionary<string, string>());
        try
        {
            var signed = await client.Storage.From(ImageBucket).CreateSignedUrls(paths.ToList(), expiresInSeconds);
            var urls = (signed ?? new())
                .Where(s => !string.IsNullOrEmpty(s.Path) && !string.IsNullOrEmpty(s.SignedUrl))
                .GroupBy(s => s.Path!)
                .ToDictionary(g => g.Key, g => g.First().SignedUrl!);
            return Response<Dictionary<string, string>>.Success(urls);
        }
        catch (Exception ex)
        {
            return Response<Dictionary<string, string>>.Failure(ex);
        }
    }

    public async Task<IResponse<long>> CreateGroup(string title, IEnumerable<string> memberIds)
    {
        try
        {
            var id = await client.Rpc<long>("create_group_conversation", new Dictionary<string, object>
            {
                ["p_title"] = title,
                ["p_member_ids"] = memberIds.ToList()
            });
            return Response<long>.Success(id);
        }
        catch (Exception ex)
        {
            return Response<long>.Failure(ex);
        }
    }

    public async Task<IResponse> AddMembers(long conversationId, IEnumerable<string> memberIds)
    {
        try
        {
            await client.Rpc("add_conversation_members", new Dictionary<string, object>
            {
                ["p_conversation_id"] = conversationId,
                ["p_member_ids"] = memberIds.ToList()
            });
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse> RenameGroup(long conversationId, string title)
    {
        try
        {
            await client.Rpc("rename_conversation", new Dictionary<string, object>
            {
                ["p_conversation_id"] = conversationId,
                ["p_title"] = title
            });
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse> LeaveGroup(long conversationId)
    {
        try
        {
            await client.Rpc("leave_conversation", new Dictionary<string, object> { ["p_conversation_id"] = conversationId });
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse> MarkRead(long conversationId)
    {
        try
        {
            await client.Rpc("mark_conversation_read", new Dictionary<string, object> { ["p_conversation_id"] = conversationId });
            return Response.Success();
        }
        catch (Exception ex)
        {
            return Response.Failure(ex.Message, ex);
        }
    }

    public async Task<IResponse<int>> GetUnreadCount()
    {
        try
        {
            return Response<int>.Success(await client.Rpc<int>("unread_message_count", null));
        }
        catch (Exception ex)
        {
            return Response<int>.Failure(ex);
        }
    }
}
