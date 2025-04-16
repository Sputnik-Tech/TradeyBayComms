using Grpc.Core;
using System.Collections.Concurrent;
using TradeyBay.Messaging.Grpc; // Your proto namespace

namespace MessagingService.Services;

public interface IConnectionManager
{
    void Subscribe(string userId, IServerStreamWriter<Message> stream);
    void Unsubscribe(string userId);
    Task BroadcastMessageAsync(Message message, IEnumerable<string> recipientUserIds);
    Task SendMessageToUserAsync(Message message, string recipientUserId);
}

// NOTE: This is a basic in-memory manager. For multi-instance scaling in AKS,
// you'll need a distributed backplane (like Azure SignalR Service with gRPC support,
// or Redis Pub/Sub, or Azure Service Bus) to route messages correctly across instances.
public class InMemoryConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, IServerStreamWriter<Message>> _connections = new();
    private readonly ILogger<InMemoryConnectionManager> _logger;

    public InMemoryConnectionManager(ILogger<InMemoryConnectionManager> logger)
    {
        _logger = logger;
    }

    public void Subscribe(string userId, IServerStreamWriter<Message> stream)
    {
        _connections.TryAdd(userId, stream);
        _logger.LogInformation("User {UserId} connection registered.", userId);
    }

    public void Unsubscribe(string userId)
    {
        _connections.TryRemove(userId, out _);
        _logger.LogInformation("User {UserId} connection removed.", userId);
    }

    public async Task BroadcastMessageAsync(Message message, IEnumerable<string> recipientUserIds)
    {
        foreach (var userId in recipientUserIds)
        {
            await SendMessageToUserAsync(message, userId);
        }
    }

    public async Task SendMessageToUserAsync(Message message, string recipientUserId)
    {
        if (_connections.TryGetValue(recipientUserId, out var stream))
        {
            try
            {
                await stream.WriteAsync(message);
                _logger.LogDebug("Message {MessageId} sent to User {UserId}", message.MessageId, recipientUserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send message {MessageId} to User {UserId}. Removing potentially dead connection.", message.MessageId, recipientUserId);
                // Attempt to remove the potentially broken stream
                Unsubscribe(recipientUserId);
            }
        }
        else
        {
             _logger.LogDebug("User {UserId} not currently connected. Message {MessageId} not sent via stream.", recipientUserId, message.MessageId);
             // Message is persisted, user will get it via GetChatHistory later
        }
    }
}