// /* using Grpc.Core;
// using Microsoft.AspNetCore.Authorization;
// using TradeyBay.Messaging.Grpc; // Your proto namespace (now includes UserIdentifier etc.)
// // Removed System.Security.Claims as we don't read them here
// using Google.Protobuf.WellKnownTypes; // For Timestamp, Empty, StringValue
// using MessagingService.Data;
// using MessagingService.Interfaces;
// using MessagingService.ExternalDTOs;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using System;
// using System.IO;
// using System.Linq;
// using System.Threading.Tasks;
// using System.Collections.Generic;
// using TradeyBay.Auth; // For Auth service proto request/response types

// namespace MessagingService.Services;

// [Authorize]
// public class MessagingServiceImpl : Messaging.MessagingBase
// {
//     private readonly MessagingDbContext _dbContext;
//     private readonly ILogger<MessagingServiceImpl> _logger;
//     private readonly IConnectionManager _connectionManager; // Uses int IDs now
//     private readonly IBlobStorageService _blobStorageService;
//     private readonly IStandardAdsGrpcClient _adsClient;
//     private readonly IAuthBusinessGrpcClient _businessClient;
//     private readonly IAuthUserProfileGrpcClient _userProfileClient;
//     // IHttpContextAccessor is removed again

//     public MessagingServiceImpl(
//         MessagingDbContext dbContext,
//         ILogger<MessagingServiceImpl> logger,
//         IConnectionManager connectionManager,
//         IBlobStorageService blobStorageService,
//         IStandardAdsGrpcClient adsClient,
//         IAuthBusinessGrpcClient businessClient,
//         IAuthUserProfileGrpcClient userProfileClient) // Removed IHttpContextAccessor
//     {
//         _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
//         _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//         _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
//         _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
//         _adsClient = adsClient ?? throw new ArgumentNullException(nameof(adsClient));
//         _businessClient = businessClient ?? throw new ArgumentNullException(nameof(businessClient));
//         _userProfileClient = userProfileClient ?? throw new ArgumentNullException(nameof(userProfileClient));
//     }

//     // Helper to get current user's INTERNAL ID via AuthService
//     // Uses the GetOrCreateAccount pattern observed in StandardAdServiceImpl
//     private async Task<int> GetCurrentInternalUserIdAsync(ServerCallContext context)
//     {
//         _logger.LogDebug("Attempting to get current user's internal ID via GetOrCreateAccountAsync.");
//         try
//         {
//             // Create an empty or minimally populated request.
//             // AuthService will use the token context to identify the user.
//             var authRequest = new GetOrCreateAccountRequest();
//             // We don't populate Email, PhoneNumber, etc. here.

//             // Call AuthService using the token propagated by the interceptor
//             var authResponse = await _userProfileClient.GetOrCreateAccountAsync(authRequest, context.CancellationToken);

//             // Check the response
//             if (authResponse?.UserProfile == null || authResponse.UserProfile.Id <= 0)
//             {
//                 _logger.LogError("GetOrCreateAccountAsync did not return a valid UserProfile with ID.");
//                 throw new RpcException(new Status(StatusCode.Unauthenticated, "Unable to identify calling user via Auth service (Invalid Profile returned)."));
//             }

//             _logger.LogDebug("GetCurrentInternalUserIdAsync successful, User ID: {UserId}", authResponse.UserProfile.Id);
//             return authResponse.UserProfile.Id; // Return the internal integer ID
//         }
//         catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
//         {
//             _logger.LogWarning("GetCurrentInternalUserIdAsync: AuthService reported Unauthenticated during GetOrCreateAccountAsync call.");
//             throw; // Re-throw specific status
//         }
//         catch (RpcException ex)
//         {
//             _logger.LogError(ex, "RpcException calling GetOrCreateAccountAsync in AuthService. Status: {StatusCode}", ex.StatusCode);
//             // Determine if this should be Unauthenticated or Internal based on the status code if needed
//              if (ex.StatusCode == StatusCode.PermissionDenied) {
//                  throw new RpcException(new Status(StatusCode.Unauthenticated, "Permission denied by Auth service."), ex.Trailers);
//              }
//             throw new RpcException(new Status(StatusCode.Internal, "Failed to communicate with Auth service to identify user."), ex.Trailers);
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Unexpected error calling GetOrCreateAccountAsync in AuthService.");
//             throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred while identifying user."), ex);
//         }
//     }


//     // --- gRPC Method Implementations ---

//     public override async Task<ChatInfo> GetOrCreateChat(GetOrCreateChatRequest request, ServerCallContext context)
//     {
//         // 1. Identify Buyer (Current User) via AuthService
//         int buyerInternalId = await GetCurrentInternalUserIdAsync(context);

//         if (string.IsNullOrEmpty(request.Context?.ListingId)) { /* ... error handling ... */ throw new RpcException(new Status(StatusCode.InvalidArgument, "Listing ID must be provided in the context.")); }
//         var listingId = request.Context.ListingId;
//         _logger.LogInformation("GetOrCreateChat called by Buyer Internal ID {BuyerInternalId} for Listing {ListingId}", buyerInternalId, listingId);

//         // 2. Get Ad Details (Seller Internal ID) from Ads Service
//         StandardAdDTO? adDetails;
//         int sellerInternalId;
//         try {
//             adDetails = await _adsClient.GetAdByIdAsync(listingId, context.CancellationToken);
//             if (adDetails == null) { throw new RpcException(new Status(StatusCode.NotFound, $"Ad listing '{listingId}' not found.")); }
//             sellerInternalId = adDetails.SellerId; // Internal ID from Ads service
//             _logger.LogInformation("Listing {ListingId} details retrieved. Seller Internal ID: {SellerId} (Type: {SellerType})", listingId, sellerInternalId, adDetails.SellerType);
//             if (buyerInternalId == sellerInternalId) { throw new RpcException(new Status(StatusCode.PermissionDenied, "Cannot initiate a chat about your own listing.")); }
//         }
//         // ... (keep existing catch blocks) ...
//          catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound) { _logger.LogWarning("Ad Service reported Ad {ListingId} not found.", listingId); throw; }
//          catch (RpcException ex) { _logger.LogError(ex, "gRPC error calling AdsService for Listing {ListingId}", listingId); throw new RpcException(new Status(StatusCode.Internal, "Failed to retrieve ad details."), ex.Trailers); }
//          catch (Exception ex) { _logger.LogError(ex, "Unexpected error retrieving ad details for Listing {ListingId}", listingId); throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred retrieving ad details.")); }

//         // Store participant IDs as strings in the DB
//         string buyerParticipantId = buyerInternalId.ToString();
//         string sellerParticipantId = sellerInternalId.ToString();

//         // 3. Check if chat already exists
//         var existingChat = await _dbContext.Chats.Include(c => c.Participants).AsNoTracking().FirstOrDefaultAsync(c => c.ListingId == listingId && c.Participants.Count == 2 && c.Participants.Any(p => p.UserId == buyerParticipantId) && c.Participants.Any(p => p.UserId == sellerParticipantId), context.CancellationToken);
//         if (existingChat != null) {
//              _logger.LogInformation("Existing chat found (ChatId: {ChatId})", existingChat.ChatId);
//              existingChat.Participants = await _dbContext.ChatParticipants.Where(p => p.ChatId == existingChat.ChatId).ToListAsync(context.CancellationToken);
//              return MapChatToChatInfo(existingChat);
//         }
//         // 4. Create new chat
//         _logger.LogInformation("Creating new chat for Listing {ListingId} between Buyer {BuyerInternalId} and Seller {SellerInternalId}", listingId, buyerInternalId, sellerInternalId);
//         var newChat = new Chat { ListingId = listingId, Participants = new List<ChatParticipant> { new ChatParticipant { UserId = buyerParticipantId }, new ChatParticipant { UserId = sellerParticipantId } } };
//         try { _dbContext.Chats.Add(newChat); await _dbContext.SaveChangesAsync(context.CancellationToken); _logger.LogInformation("New chat created successfully (ChatId: {ChatId})", newChat.ChatId); newChat.Participants = await _dbContext.ChatParticipants.Where(p => p.ChatId == newChat.ChatId).ToListAsync(context.CancellationToken); return MapChatToChatInfo(newChat); }
//         catch (DbUpdateException ex) { _logger.LogError(ex, "Failed to save new chat for Listing {ListingId}", listingId); throw new RpcException(new Status(StatusCode.Internal, "Failed to create chat database entry.")); }
//     }

//      public override async Task<Message> SendMessage(SendMessageRequest request, ServerCallContext context)
//      {
//         // 1. Identify Sender via AuthService
//         int senderInternalId = await GetCurrentInternalUserIdAsync(context);
//         string senderParticipantId = senderInternalId.ToString();

//         if (string.IsNullOrEmpty(request.ChatId) || !Guid.TryParse(request.ChatId, out var chatIdGuid)) { throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid ChatId is required.")); }
//          _logger.LogInformation("SendMessage attempt by User Internal ID {SenderInternalId} to Chat {ChatId}. Acting as Business: {ActingAsBusinessId}", senderInternalId, request.ChatId, request.ActingAsBusinessId ?? "N/A");

//         // 2. Validate participation & get recipient (using string IDs)
//         var chatParticipants = await _dbContext.ChatParticipants.Where(p => p.ChatId == chatIdGuid).Select(p => p.UserId).ToListAsync(context.CancellationToken);
//         if (!chatParticipants.Contains(senderParticipantId)) { throw new RpcException(new Status(StatusCode.PermissionDenied, "User is not a participant in this chat.")); }
//         var recipientParticipantId = chatParticipants.FirstOrDefault(id => id != senderParticipantId); // Internal ID as string

//         // 3. Validate Business Representation (if applicable)
//         string? actingAsBusinessIdString = string.IsNullOrEmpty(request.ActingAsBusinessId) ? null : request.ActingAsBusinessId;
//         int? actingAsBusinessIdInt = null;
//         if (actingAsBusinessIdString != null) {
//             if (!int.TryParse(actingAsBusinessIdString, out var parsedBusinessId)) { throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid Business ID format provided.")); }
//             actingAsBusinessIdInt = parsedBusinessId;
//             try {
//                  var userBusinessesRequest = new GetBusinessesByUserRequest { UserProfileId = senderInternalId }; // Use internal ID
//                  var userBusinessesResponse = await _businessClient.GetBusinessesByUserAsync(userBusinessesRequest); // Use interface method
//                  bool isAuthorized = userBusinessesResponse?.Businesses?.Any(b => b.Id == actingAsBusinessIdInt) ?? false;
//                  if (!isAuthorized) { throw new RpcException(new Status(StatusCode.PermissionDenied, $"User {senderInternalId} is not authorized to represent Business {actingAsBusinessIdInt}.")); }
//                   _logger.LogInformation("User {SenderInternalId} validated as representative for Business {BusinessId}", senderInternalId, actingAsBusinessIdInt);
//             }
//             // ... (keep existing catch blocks for business validation) ...
//              catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated || ex.StatusCode == StatusCode.PermissionDenied) { _logger.LogWarning("Auth error calling AuthService validating Business {BusinessId} for User {UserId}: {Status}", actingAsBusinessIdString, senderInternalId, ex.Status); throw; }
//              catch (RpcException ex) { _logger.LogError(ex, "gRPC error calling AuthService validating business representation for User {SenderInternalId}, Business {BusinessId}", senderInternalId, actingAsBusinessIdString); throw new RpcException(new Status(StatusCode.Internal, "Failed to validate business representation.")); }
//              catch (Exception ex) { _logger.LogError(ex, "Unexpected error validating business representation for User {SenderInternalId}, Business {BusinessId}", senderInternalId, actingAsBusinessIdString); throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred during validation.")); }
//         }

//         // 4. Create and Save Message Entity (using string IDs)
//         var messageEntity = new ChatMessage { ChatId = chatIdGuid, SenderUserId = senderParticipantId, SenderActingAsBusinessId = actingAsBusinessIdString, Content = request.Content, MessageType = MapProtoMessageTypeToDb(request.MessageType), Timestamp = DateTime.UtcNow, MediaUrl = request.MediaInfo?.MediaUrl, MediaFileName = request.MediaInfo?.FileName, MediaMimeType = request.MediaInfo?.MimeType, MediaSizeBytes = request.MediaInfo?.SizeBytes };
//         try { _dbContext.Messages.Add(messageEntity); await _dbContext.SaveChangesAsync(context.CancellationToken); _logger.LogInformation("Message {MessageId} saved to Chat {ChatId}", messageEntity.MessageId, request.ChatId); }
//         catch (DbUpdateException ex) { _logger.LogError(ex, "Failed to save message for Chat {ChatId}", request.ChatId); throw new RpcException(new Status(StatusCode.Internal, "Failed to save message database entry.")); }

//         // 5. Distribute message via Connection Manager (using internal int IDs)
//         var messageProto = MapMessageToProto(messageEntity);
//         if (!string.IsNullOrEmpty(recipientParticipantId) && int.TryParse(recipientParticipantId, out int recipientInternalId)) {
//             await _connectionManager.SendMessageToUserAsync(messageProto, recipientInternalId); // Use int ID with manager
//              _logger.LogInformation("Message {MessageId} queued for distribution to Recipient Internal ID {RecipientInternalId}", messageEntity.MessageId, recipientInternalId);
//         } else {
//              _logger.LogWarning("Recipient internal ID ({RecipientParticipantId}) could not be determined or parsed for Chat {ChatId}, message {MessageId}. Not distributed in real-time.", recipientParticipantId, request.ChatId, messageEntity.MessageId);
//         }

//         // 6. Return the saved message
//         return messageProto;
//      }


//      public override async Task SubscribeToMessages(SubscribeRequest request, IServerStreamWriter<Message> responseStream, ServerCallContext context)
//      {
//         // 1. Identify User via AuthService
//         int internalUserId = await GetCurrentInternalUserIdAsync(context);
//         var connectionId = context.Peer;
//         _logger.LogInformation("User Internal ID {InternalUserId} subscribing to messages. ConnectionId: {ConnectionId}", internalUserId, connectionId);
//         try {
//              _connectionManager.Subscribe(internalUserId, responseStream); // Use int ID
//              await context.CancellationToken.AsTask();
//         }
//         // ... (keep existing finally/catch blocks) ...
//         catch (OperationCanceledException) { _logger.LogInformation("Subscription cancelled for User Internal ID {InternalUserId}. ConnectionId: {ConnectionId}", internalUserId, connectionId); }
//         catch (Exception ex) { _logger.LogError(ex, "Error during message subscription for User Internal ID {InternalUserId}. ConnectionId: {ConnectionId}", internalUserId, connectionId); }
//         finally { _connectionManager.Unsubscribe(internalUserId); _logger.LogInformation("User Internal ID {InternalUserId} unsubscribed. ConnectionId: {ConnectionId}", internalUserId, connectionId); }
//      }

//      public override async Task<GetChatHistoryResponse> GetChatHistory(GetChatHistoryRequest request, ServerCallContext context)
//      {
//         // 1. Identify User via AuthService
//          int userInternalId = await GetCurrentInternalUserIdAsync(context);
//          string userParticipantId = userInternalId.ToString();

//          if (!Guid.TryParse(request.ChatId, out var chatIdGuid)) { throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid ChatId is required.")); }
//           _logger.LogInformation("GetChatHistory requested by User Internal ID {UserInternalId} for Chat {ChatId}", userInternalId, request.ChatId);

//         // 2. Validate participation (using string ID)
//           var isParticipant = await _dbContext.ChatParticipants.AnyAsync(p => p.ChatId == chatIdGuid && p.UserId == userParticipantId, context.CancellationToken);
//           if (!isParticipant) { throw new RpcException(new Status(StatusCode.PermissionDenied, "User is not a participant in this chat.")); }

//          // 3. Query messages (logic same)
//          // ... (query logic remains unchanged) ...
//          var query = _dbContext.Messages.Where(m => m.ChatId == chatIdGuid).OrderByDescending(m => m.Timestamp);
//          ChatMessage? cursorMessage = null;
//          if(!string.IsNullOrEmpty(request.BeforeMessageId) && Guid.TryParse(request.BeforeMessageId, out var cursorMessageIdGuid)) { cursorMessage = await _dbContext.Messages.FindAsync(new object[] { cursorMessageIdGuid }, context.CancellationToken); }
//          if(cursorMessage != null) { query = query.Where(m => m.Timestamp < cursorMessage.Timestamp).OrderByDescending(m => m.Timestamp); }
//          var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 50);
//          var messages = await query.Take(pageSize + 1).ToListAsync(context.CancellationToken);
//          var hasMoreMessages = messages.Count > pageSize;
//          var responseMessages = messages.Take(pageSize).ToList();
//           _logger.LogInformation("Retrieved {MessageCount} messages for Chat {ChatId}. HasMore: {HasMore}", responseMessages.Count, request.ChatId, hasMoreMessages);

//          // 4. Map and return (Use updated mapper)
//          return new GetChatHistoryResponse { Messages = { responseMessages.Select(MapMessageToProto) }, HasMoreMessages = hasMoreMessages };
//      }


//      public override async Task<GetMediaUploadUrlResponse> GetMediaUploadUrl(GetMediaUploadUrlRequest request, ServerCallContext context)
//      {
//         // 1. Identify User via AuthService
//          int userInternalId = await GetCurrentInternalUserIdAsync(context);
//          string userParticipantId = userInternalId.ToString();

//          if (!Guid.TryParse(request.ChatId, out var chatIdGuid)) { throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid ChatId is required for context.")); }
//          _logger.LogInformation("GetMediaUploadUrl requested by User Internal ID {UserInternalId} for Chat {ChatId}, File: {FileName}", userInternalId, request.ChatId, request.FileName);

//         // 2. Validate participation (using string ID)
//          var isParticipant = await _dbContext.ChatParticipants.AnyAsync(p => p.ChatId == chatIdGuid && p.UserId == userParticipantId, context.CancellationToken);
//          if (!isParticipant) { throw new RpcException(new Status(StatusCode.PermissionDenied, "User is not a participant in the specified chat.")); }

//         // 3. Generate Blob Name (using internal ID)
//          var safeFileName = Path.GetFileName(request.FileName ?? "file");
//          var blobName = $"chats/{chatIdGuid}/{userInternalId}/{Guid.NewGuid()}_{safeFileName}";

//         // 4. Get SAS URI
//         try {
//              var expiration = TimeSpan.FromMinutes(15);
//              var sasUri = await _blobStorageService.GetUploadSasUriAsync(blobName, expiration);
//              var downloadUrl = _blobStorageService.GetDownloadUrl(blobName);
//              _logger.LogInformation("Generated SAS Upload URL for Blob {BlobName}", blobName);
//              return new GetMediaUploadUrlResponse { UploadUrl = sasUri.ToString(), BlobName = blobName, MediaUrl = downloadUrl };
//         }
//         // ... (keep existing catch blocks) ...
//         catch (InvalidOperationException ex) { _logger.LogError(ex, "Failed to generate SAS URI for Blob {BlobName}", blobName); throw new RpcException(new Status(StatusCode.Internal, "Failed to generate upload URL due to configuration issue.")); }
//         catch (Exception ex) { _logger.LogError(ex, "Failed to generate SAS URI for Blob {BlobName}", blobName); throw new RpcException(new Status(StatusCode.Internal, "Failed to generate upload URL.")); }
//      }

//     // TODO: Implement InjectSystemMessage


//     // --- Mappers (Updated for int32 UserIdentifier) ---

//     private ChatInfo MapChatToChatInfo(Chat chat) {
//         return new ChatInfo { // Type from TradeyBay.Messaging.Grpc
//             ChatId = chat.ChatId.ToString(),
//             Context = new ContextInfo { ListingId = chat.ListingId ?? "" }, // Local ContextInfo
//             // Participants UserId are strings in DB, parse back to int for proto
//             Participants = { chat.Participants.Select(p => new UserIdentifier { UserId = int.TryParse(p.UserId, out var id) ? id : 0 }) } // Local UserIdentifier (int32)
//         };
//     }

//     private Message MapMessageToProto(ChatMessage entity) {
//         return new Message { // Type from TradeyBay.Messaging.Grpc
//             MessageId = entity.MessageId.ToString(),
//             ChatId = entity.ChatId.ToString(),
//             SenderInfo = new SenderInfo { // Local SenderInfo
//                 // SenderUserId is string in DB, parse back to int for proto
//                 User = new UserIdentifier{ UserId = int.TryParse(entity.SenderUserId, out var id) ? id : 0 }, // Local UserIdentifier (int32)
//                 ActingAsBusinessId = entity.SenderActingAsBusinessId ?? ""
//             },
//             Content = entity.Content ?? "",
//             Timestamp = Timestamp.FromDateTime(entity.Timestamp.ToUniversalTime()),
//             MessageType = MapDbMessageTypeToProto(entity.MessageType), // Use local mapping helper
//             MediaInfo = entity.MessageType == DbMessageType.MEDIA ? new MediaInfo { // Local MediaInfo
//                 MediaUrl = entity.MediaUrl ?? "", FileName = entity.MediaFileName ?? "", MimeType = entity.MediaMimeType ?? "", SizeBytes = entity.MediaSizeBytes ?? 0 } : null
//         };
//     }

//      private DbMessageType MapProtoMessageTypeToDb(MessageType protoType) { // Input is local MessageType
//          return protoType switch {
//              MessageType.TEXT => DbMessageType.TEXT, MessageType.MEDIA => DbMessageType.MEDIA, MessageType.SYSTEM => DbMessageType.SYSTEM, MessageType.CALL_LOG => DbMessageType.CALL_LOG,
//              _ => throw new ArgumentOutOfRangeException(nameof(protoType), $"Unsupported message type: {protoType}") };
//      }

//       private MessageType MapDbMessageTypeToProto(DbMessageType dbType) { // Return type is local MessageType
//          return dbType switch {
//              DbMessageType.TEXT => MessageType.TEXT, DbMessageType.MEDIA => MessageType.MEDIA, DbMessageType.SYSTEM => MessageType.SYSTEM, DbMessageType.CALL_LOG => MessageType.CALL_LOG,
//              _ => throw new ArgumentOutOfRangeException(nameof(dbType), $"Unsupported message type: {dbType}") };
//      }
// }

// // Helper Extension (remains the same)
// public static class CancellationTokenExtensions {
//     public static Task AsTask(this CancellationToken cancellationToken) {
//         var tcs = new TaskCompletionSource<object>();
//         cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
//         return tcs.Task;
//     }
// } */