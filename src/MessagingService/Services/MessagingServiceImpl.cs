using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using TradeyBay.Messaging.Grpc; // Your proto namespace
using System.Security.Claims;
using Google.Protobuf.WellKnownTypes;
using MessagingService.Data; // Your EF Core models namespace
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Use Microsoft.Extensions.Logging
using System; // For Guid, DateTime etc.
using System.Linq; // For LINQ queries
using System.Threading.Tasks; // For Task
using Common; // Assuming common proto types are here if not qualified
// TODO: Add using statements for Auth and Ads gRPC client namespaces and models
// using YourAuthServiceNamespace;
// using YourAdsServiceNamespace;


namespace MessagingService.Services;

[Authorize] // Ensures user is authenticated via B2C token for all calls
public class MessagingServiceImpl : Messaging.MessagingBase
{
    private readonly MessagingDbContext _dbContext;
    private readonly ILogger<MessagingServiceImpl> _logger;
    private readonly IConnectionManager _connectionManager;
    private readonly IBlobStorageService _blobStorageService;
    // TODO: Inject gRPC Clients
    private readonly AuthService.AuthServiceClient _authClient; // Replace with actual type
    private readonly AdsService.AdsServiceClient _adsClient;   // Replace with actual type
    private readonly IHttpContextAccessor _httpContextAccessor; // To get user ID easily

    public MessagingServiceImpl(
        MessagingDbContext dbContext,
        ILogger<MessagingServiceImpl> logger,
        IConnectionManager connectionManager,
        IBlobStorageService blobStorageService,
        AuthService.AuthServiceClient authClient, // Inject clients
        AdsService.AdsServiceClient adsClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _logger = logger;
        _connectionManager = connectionManager;
        _blobStorageService = blobStorageService;
        _authClient = authClient;
        _adsClient = adsClient;
        _httpContextAccessor = httpContextAccessor;
    }

    // Helper to get User ID from the authenticated context
    private string GetUserId()
    {
        // ClaimTypes.NameIdentifier is standard for unique ID (maps to 'sub' usually)
        // 'oid' is the Azure AD Object ID claim
        var userId = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("oid");

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("User identifier claim ('sub' or 'oid') not found in token.");
            throw new RpcException(new Status(StatusCode.Unauthenticated, "User identifier not found in token."));
        }
        _logger.LogDebug("Authenticated User ID: {UserId}", userId);
        return userId;
    }

    // --- gRPC Method Implementations ---

    public override async Task<ChatInfo> GetOrCreateChat(GetOrCreateChatRequest request, ServerCallContext context)
    {
        var buyerUserId = GetUserId(); // The user calling this is the buyer initiating

        if (string.IsNullOrEmpty(request.Context?.ListingId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Listing ID must be provided in the context."));
        }
        var listingId = request.Context.ListingId;
        _logger.LogInformation("GetOrCreateChat called by Buyer {BuyerUserId} for Listing {ListingId}", buyerUserId, listingId);

        // 1. Get Seller Info from Ads Service
        string sellerUserId;
        try
        {
             // TODO: Replace with actual AdsService call and request/response structure
            var adDetailsRequest = new GetAdDetailsRequest { AdId = listingId }; // Assuming this request structure
            var adDetailsResponse = await _adsClient.GetAdDetailsAsync(adDetailsRequest, context.RequestHeaders); // Pass headers for auth propagation if needed
            if (adDetailsResponse == null || string.IsNullOrEmpty(adDetailsResponse.SellerUserId))
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Ad listing '{listingId}' not found or has no seller info."));
            }
            sellerUserId = adDetailsResponse.SellerUserId;
             _logger.LogInformation("Listing {ListingId} belongs to Seller {SellerUserId}", listingId, sellerUserId);

             // Prevent user from chatting with themselves
             if (buyerUserId == sellerUserId)
             {
                  throw new RpcException(new Status(StatusCode.PermissionDenied, "Cannot initiate a chat with yourself."));
             }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated || ex.StatusCode == StatusCode.PermissionDenied)
        {
             _logger.LogWarning("Auth error calling AdsService for Listing {ListingId}: {Status}", listingId, ex.Status);
             throw; // Re-throw auth errors
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "gRPC error calling AdsService for Listing {ListingId}", listingId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to retrieve ad details."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving ad details for Listing {ListingId}", listingId);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }


        // 2. Check if chat already exists for this buyer, seller, and listing
        var existingChat = await _dbContext.Chats
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.ListingId == listingId &&
                                      c.Participants.Count == 2 && // Ensure it's the correct 1:1 chat
                                      c.Participants.Any(p => p.UserId == buyerUserId) &&
                                      c.Participants.Any(p => p.UserId == sellerUserId),
                                      context.CancellationToken);

        if (existingChat != null)
        {
            _logger.LogInformation("Existing chat found (ChatId: {ChatId}) for Listing {ListingId} between Buyer {BuyerUserId} and Seller {SellerUserId}", existingChat.ChatId, listingId, buyerUserId, sellerUserId);
            return MapChatToChatInfo(existingChat);
        }

        // 3. Create new chat if not found
        _logger.LogInformation("Creating new chat for Listing {ListingId} between Buyer {BuyerUserId} and Seller {SellerUserId}", listingId, buyerUserId, sellerUserId);
        var newChat = new Chat
        {
            ListingId = listingId,
            Participants = new List<ChatParticipant>
            {
                new ChatParticipant { UserId = buyerUserId },
                new ChatParticipant { UserId = sellerUserId }
            }
            // CreatedAt is set by default
        };

        try
        {
            _dbContext.Chats.Add(newChat);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("New chat created successfully (ChatId: {ChatId})", newChat.ChatId);
            return MapChatToChatInfo(newChat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save new chat for Listing {ListingId}", listingId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to create chat."));
        }
    }

     public override async Task<Message> SendMessage(SendMessageRequest request, ServerCallContext context)
     {
        var senderUserId = GetUserId();
        if (string.IsNullOrEmpty(request.ChatId) || !Guid.TryParse(request.ChatId, out var chatIdGuid))
        {
             throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid ChatId is required."));
        }
         _logger.LogInformation("SendMessage attempt by User {SenderUserId} to Chat {ChatId}. Acting as Business: {ActingAsBusinessId}",
             senderUserId, request.ChatId, request.ActingAsBusinessId ?? "N/A");

        // 1. Validate user is participant and get other participant ID
        var chatParticipants = await _dbContext.ChatParticipants
            .Where(p => p.ChatId == chatIdGuid)
            .Select(p => p.UserId)
            .ToListAsync(context.CancellationToken);

        if (!chatParticipants.Contains(senderUserId))
        {
             throw new RpcException(new Status(StatusCode.PermissionDenied, "User is not a participant in this chat."));
        }
        if (chatParticipants.Count != 2)
        {
             _logger.LogWarning("Chat {ChatId} does not have exactly two participants.", request.ChatId);
             // This indicates a potential data inconsistency, but proceed for now. Handle as needed.
        }
        var recipientUserId = chatParticipants.FirstOrDefault(id => id != senderUserId);


         // 2. Validate Business Representation (if applicable)
         if (!string.IsNullOrEmpty(request.ActingAsBusinessId))
         {
             try
             {
                  // TODO: Replace with actual AuthService call
                 var validationRequest = new ValidateUserBusinessMembershipRequest { UserId = senderUserId, BusinessId = request.ActingAsBusinessId };
                 var validationResponse = await _authClient.ValidateUserBusinessMembershipAsync(validationRequest, context.RequestHeaders); // Pass headers if needed
                 if (validationResponse == null || !validationResponse.IsMember)
                 {
                     throw new RpcException(new Status(StatusCode.PermissionDenied, $"User {senderUserId} is not authorized to represent Business {request.ActingAsBusinessId}."));
                 }
                 _logger.LogInformation("User {SenderUserId} validated as representative for Business {BusinessId}", senderUserId, request.ActingAsBusinessId);
             }
             catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated || ex.StatusCode == StatusCode.PermissionDenied)
            {
                _logger.LogWarning("Auth error calling AuthService validating Business {BusinessId} for User {UserId}: {Status}", request.ActingAsBusinessId, senderUserId, ex.Status);
                throw; // Re-throw auth errors
            }
             catch (RpcException ex)
             {
                  _logger.LogError(ex, "gRPC error calling AuthService validating business representation for User {SenderUserId}, Business {BusinessId}", senderUserId, request.ActingAsBusinessId);
                 throw new RpcException(new Status(StatusCode.Internal, "Failed to validate business representation."));
             }
              catch (Exception ex)
             {
                  _logger.LogError(ex, "Unexpected error validating business representation for User {SenderUserId}, Business {BusinessId}", senderUserId, request.ActingAsBusinessId);
                 throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred during validation."));
             }
         }

        // 3. Create and Save Message Entity
        var messageEntity = new ChatMessage
        {
            ChatId = chatIdGuid,
            SenderUserId = senderUserId,
            SenderActingAsBusinessId = string.IsNullOrEmpty(request.ActingAsBusinessId) ? null : request.ActingAsBusinessId,
            Content = request.Content,
            MessageType = MapProtoMessageTypeToDb(request.MessageType),
            Timestamp = DateTime.UtcNow, // Use server timestamp
            MediaUrl = request.MediaInfo?.MediaUrl,
            MediaFileName = request.MediaInfo?.FileName,
            MediaMimeType = request.MediaInfo?.MimeType,
            MediaSizeBytes = request.MediaInfo?.SizeBytes
        };

        try
        {
             _dbContext.Messages.Add(messageEntity);
             await _dbContext.SaveChangesAsync(context.CancellationToken);
             _logger.LogInformation("Message {MessageId} saved to Chat {ChatId}", messageEntity.MessageId, request.ChatId);
        }
         catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to save message for Chat {ChatId}", request.ChatId);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to save message."));
        }

        // 4. Distribute message via Connection Manager
        var messageProto = MapMessageToProto(messageEntity);
        if (!string.IsNullOrEmpty(recipientUserId))
        {
            await _connectionManager.SendMessageToUserAsync(messageProto, recipientUserId);
             _logger.LogInformation("Message {MessageId} queued for distribution to User {RecipientUserId}", messageEntity.MessageId, recipientUserId);
        } else {
             _logger.LogWarning("Recipient could not be determined for Chat {ChatId}, message {MessageId} not distributed in real-time.", request.ChatId, messageEntity.MessageId);
        }


        // 5. Return the saved message
        return messageProto;
     }


     public override async Task SubscribeToMessages(SubscribeRequest request, IServerStreamWriter<Message> responseStream, ServerCallContext context)
     {
        var userId = GetUserId();
        var connectionId = context.Peer; // Unique identifier for the connection
         _logger.LogInformation("User {UserId} subscribing to messages. ConnectionId: {ConnectionId}", userId, connectionId);

        try
        {
             _connectionManager.Subscribe(userId, responseStream);

             // Keep the connection alive until cancelled
             await context.CancellationToken.AsTask();
        }
        catch (OperationCanceledException)
        {
              _logger.LogInformation("Subscription cancelled for User {UserId}. ConnectionId: {ConnectionId}", userId, connectionId);
        }
        catch (Exception ex)
        {
             // This might catch exceptions if WriteAsync fails within the ConnectionManager
              _logger.LogError(ex, "Error during message subscription for User {UserId}. ConnectionId: {ConnectionId}", userId, connectionId);
        }
         finally
         {
             _connectionManager.Unsubscribe(userId);
              _logger.LogInformation("User {UserId} unsubscribed. ConnectionId: {ConnectionId}", userId, connectionId);
         }
     }

     public override async Task<GetChatHistoryResponse> GetChatHistory(GetChatHistoryRequest request, ServerCallContext context)
     {
         var userId = GetUserId();
         if (!Guid.TryParse(request.ChatId, out var chatIdGuid))
         {
             throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid ChatId is required."));
         }

         _logger.LogInformation("GetChatHistory requested by User {UserId} for Chat {ChatId}. PageSize: {PageSize}, BeforeMessageId: {BeforeMessageId}",
            userId, request.ChatId, request.PageSize, request.BeforeMessageId ?? "N/A");

         // 1. Validate user participation
          var isParticipant = await _dbContext.ChatParticipants
              .AnyAsync(p => p.ChatId == chatIdGuid && p.UserId == userId, context.CancellationToken);
          if (!isParticipant)
          {
               throw new RpcException(new Status(StatusCode.PermissionDenied, "User is not a participant in this chat."));
          }

         // 2. Query messages with pagination
         var query = _dbContext.Messages
             .Where(m => m.ChatId == chatIdGuid)
             .OrderByDescending(m => m.Timestamp); // Order by newest first

         // Cursor Pagination Logic (using timestamp or message ID)
          // Using message ID assumes Guids/UUIDs are sortable chronologically (like UUIDv1 or v7) or based on Timestamp
         ChatMessage? cursorMessage = null;
         if(!string.IsNullOrEmpty(request.BeforeMessageId) && Guid.TryParse(request.BeforeMessageId, out var cursorMessageIdGuid))
         {
             cursorMessage = await _dbContext.Messages.FindAsync(new object[] { cursorMessageIdGuid }, context.CancellationToken);
         }

         if(cursorMessage != null)
         {
              // Fetch messages *before* the cursor message's timestamp
             query = query.Where(m => m.Timestamp < cursorMessage.Timestamp)
                          .OrderByDescending(m => m.Timestamp); // Maintain order
         }


         // Ensure PageSize has a reasonable default and max limit
         var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 50); // Example: Default 20, Max 50

         var messages = await query
             .Take(pageSize + 1) // Fetch one extra message to check if there are more
             .ToListAsync(context.CancellationToken);

         var hasMoreMessages = messages.Count > pageSize;
         var responseMessages = messages.Take(pageSize).ToList(); // Take only the requested page size

          _logger.LogInformation("Retrieved {MessageCount} messages for Chat {ChatId}. HasMore: {HasMore}", responseMessages.Count, request.ChatId, hasMoreMessages);


         // 3. Map and return
         return new GetChatHistoryResponse
         {
             Messages = { responseMessages.Select(MapMessageToProto) },
             HasMoreMessages = hasMoreMessages
             // If using continuation token, generate one based on the last message retrieved
             // NextContinuationToken = responseMessages.LastOrDefault()?.MessageId.ToString() ?? string.Empty
         };
     }


     public override async Task<GetMediaUploadUrlResponse> GetMediaUploadUrl(GetMediaUploadUrlRequest request, ServerCallContext context)
     {
        var userId = GetUserId();
         if (!Guid.TryParse(request.ChatId, out var chatIdGuid))
         {
             throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid ChatId is required for context."));
         }

         _logger.LogInformation("GetMediaUploadUrl requested by User {UserId} for Chat {ChatId}, File: {FileName}", userId, request.ChatId, request.FileName);


        // 1. Validate user participation (optional but recommended)
         var isParticipant = await _dbContext.ChatParticipants
             .AnyAsync(p => p.ChatId == chatIdGuid && p.UserId == userId, context.CancellationToken);
         if (!isParticipant)
         {
              throw new RpcException(new Status(StatusCode.PermissionDenied, "User is not a participant in the specified chat."));
         }

        // 2. Generate Blob Name
         // Ensure filename is safe for URLs/paths
         var safeFileName = Path.GetFileName(request.FileName ?? "file"); // Basic sanitization
         var blobName = $"chats/{chatIdGuid}/{userId}/{Guid.NewGuid()}_{safeFileName}"; // Example structure

        // 3. Get SAS URI from Blob Service
        try
        {
             var expiration = TimeSpan.FromMinutes(15); // Make SAS token short-lived
             var sasUri = await _blobStorageService.GetUploadSasUriAsync(blobName, expiration);
             var downloadUrl = _blobStorageService.GetDownloadUrl(blobName); // The final URL after upload

            _logger.LogInformation("Generated SAS Upload URL for Blob {BlobName}", blobName);

             return new GetMediaUploadUrlResponse
             {
                 UploadUrl = sasUri.ToString(),
                 BlobName = blobName,
                 MediaUrl = downloadUrl
             };
        }
        catch (InvalidOperationException ex)
        {
             _logger.LogError(ex, "Failed to generate SAS URI, potentially configuration issue for Blob {BlobName}", blobName);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to generate upload URL due to configuration issue."));
        }
         catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to generate SAS URI for Blob {BlobName}", blobName);
            throw new RpcException(new Status(StatusCode.Internal, "Failed to generate upload URL."));
        }
     }

    // TODO: Implement InjectSystemMessage (consider different auth mechanism if needed)


    // --- Mappers ---

    private ChatInfo MapChatToChatInfo(Chat chat)
    {
        return new ChatInfo
        {
            ChatId = chat.ChatId.ToString(),
            Context = new Common.ContextInfo { ListingId = chat.ListingId }, // Assuming ContextInfo only has ListingId now
            Participants = { chat.Participants.Select(p => new Common.UserIdentifier { UserId = p.UserId }) }
        };
    }

    private Message MapMessageToProto(ChatMessage entity)
    {
        return new Message
        {
            MessageId = entity.MessageId.ToString(),
            ChatId = entity.ChatId.ToString(),
            SenderInfo = new SenderInfo {
                User = new Common.UserIdentifier{ UserId = entity.SenderUserId },
                ActingAsBusinessId = entity.SenderActingAsBusinessId ?? "" // Use "" for optional string if null
            },
            Content = entity.Content ?? "",
            Timestamp = Timestamp.FromDateTime(entity.Timestamp.ToUniversalTime()), // Ensure UTC
            MessageType = MapDbMessageTypeToProto(entity.MessageType),
            MediaInfo = entity.MessageType == DbMessageType.MEDIA ? new Common.MediaInfo
            {
                MediaUrl = entity.MediaUrl ?? "",
                FileName = entity.MediaFileName ?? "",
                MimeType = entity.MediaMimeType ?? "",
                SizeBytes = entity.MediaSizeBytes ?? 0
            } : null
        };
    }

     private DbMessageType MapProtoMessageTypeToDb(Common.MessageType protoType)
     {
         return protoType switch
         {
             Common.MessageType.TEXT => DbMessageType.TEXT,
             Common.MessageType.MEDIA => DbMessageType.MEDIA,
             Common.MessageType.SYSTEM => DbMessageType.SYSTEM,
             Common.MessageType.CALL_LOG => DbMessageType.CALL_LOG,
             _ => throw new ArgumentOutOfRangeException(nameof(protoType), $"Unsupported message type: {protoType}")
         };
     }

      private Common.MessageType MapDbMessageTypeToProto(DbMessageType dbType)
     {
         return dbType switch
         {
             DbMessageType.TEXT => Common.MessageType.TEXT,
             DbMessageType.MEDIA => Common.MessageType.MEDIA,
             DbMessageType.SYSTEM => Common.MessageType.SYSTEM,
             DbMessageType.CALL_LOG => Common.MessageType.CALL_LOG,
             _ => throw new ArgumentOutOfRangeException(nameof(dbType), $"Unsupported message type: {dbType}")
         };
     }
}

// Helper Extension for CancellationToken.AsTask()
public static class CancellationTokenExtensions
{
    public static Task AsTask(this CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object>();
        cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
        return tcs.Task;
    }
}