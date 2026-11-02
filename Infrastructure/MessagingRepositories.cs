using Social.Core.Interfaces.Infrastructure;
using Social.Core.Interfaces.Response;
using Social.Core.Models.Database;
using Social.Core.Models.Response;
using static Supabase.Postgrest.Constants;

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

    public async Task<IResponse<string>> GetPartnerId(long conversationId, string currentUserId)
    {
        try
        {
            // RLS (016): nur Mitglieder sehen die Mitgliederliste
            var result = await client.From<ConversationMemberRecord>()
                .Where(x => x.ConversationId == conversationId)
                .Get();
            var partner = result.Models.FirstOrDefault(m => m.UserId != currentUserId);
            return partner is not null ? Response<string>.Success(partner.UserId) : Response<string>.Failure("Conversation not found");
        }
        catch (Exception ex)
        {
            return Response<string>.Failure(ex);
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

    public async Task<IResponse<List<MessageRecord>>> GetMessagesAfter(long conversationId, long afterId)
    {
        try
        {
            var result = await client.From<MessageRecord>()
                .Where(x => x.ConversationId == conversationId && x.Id > afterId)
                .Order(x => x.Id, Ordering.Ascending)
                .Get();
            return Response<List<MessageRecord>>.Success(result.Models);
        }
        catch (Exception ex)
        {
            return Response<List<MessageRecord>>.Failure(ex);
        }
    }

    public async Task<IResponse<MessageRecord>> SendMessage(long conversationId, string senderId, string content)
    {
        try
        {
            var result = await client.From<MessageRecord>()
                .Insert(new MessageRecord { ConversationId = conversationId, SenderId = senderId, Content = content });
            return result.Model is not null ? Response<MessageRecord>.Success(result.Model) : Response<MessageRecord>.Failure("Message not sent");
        }
        catch (Exception ex)
        {
            return Response<MessageRecord>.Failure(ex);
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
