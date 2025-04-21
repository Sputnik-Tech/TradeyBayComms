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



// using Grpc.Core;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Security.Claims;
// using System.Threading.Tasks;
// using Google.Protobuf.WellKnownTypes; // For Empty and Timestamp
// using MessagingService.Data;
// using MessagingService.Interfaces;
// using MessagingService.Services;
// using TradeyBay.Messaging.Grpc; // Your proto namespace for Messaging
// using TradeyBay.Auth;          // Your proto namespace for Auth
// // Remove duplicate using statement for TradeyBay.StandardAds if present
// using TradeyBay.StandardAds;    // Your proto namespace for StandardAds (already referenced via IStandardAdsGrpcClient)

// namespace MessagingService.Services // Or your preferred namespace
// {
//     [Authorize] // Require authentication for all methods by default
//     public class MessagingServiceImpl : Messaging.MessagingBase // Inherit from the generated base class
//     {
//         private readonly MessagingDbContext _dbContext;
//         private readonly ILogger<MessagingServiceImpl> _logger;
//         private readonly IHttpContextAccessor _httpContextAccessor;
//         private readonly IAuthUserProfileGrpcClient _authProfileClient;
//         private readonly IStandardAdsGrpcClient _standardAdsClient;
//         private readonly IConnectionManager _connectionManager;
//         private readonly IBlobStorageService _blobStorageService;
//         // private readonly AuthUserProfileMapper _userProfileMapper; // If you have it

//         // Inject necessary dependencies
//         public MessagingServiceImpl(
//             MessagingDbContext dbContext,
//             ILogger<MessagingServiceImpl> logger,
//             IHttpContextAccessor httpContextAccessor,
//             IAuthUserProfileGrpcClient authProfileClient,
//             IStandardAdsGrpcClient standardAdsClient,
//             IConnectionManager connectionManager,
//             IBlobStorageService blobStorageService
//             /* AuthUserProfileMapper userProfileMapper */)
//         {
//             _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
//             _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//             _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
//             _authProfileClient = authProfileClient ?? throw new ArgumentNullException(nameof(authProfileClient));
//             _standardAdsClient = standardAdsClient ?? throw new ArgumentNullException(nameof(standardAdsClient));
//             _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
//             _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
//             // _userProfileMapper = userProfileMapper ?? throw new ArgumentNullException(nameof(userProfileMapper));
//         }

//         // Helper to get the current user's B2C Object ID (OID)
//         private string GetCurrentUserId()
//         {
//             // Adjust the claim type if necessary based on your B2C configuration (oid or sub)
//             var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) // Common claim for object ID
//                 ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("oid"); // Another common claim name

//             if (string.IsNullOrEmpty(userIdClaim))
//             {
//                 _logger.LogWarning("Could not find User ID claim (NameIdentifier or oid) in the current HttpContext.");
//                 throw new RpcException(new Status(StatusCode.Unauthenticated, "User ID claim not found."));
//             }
//             return userIdClaim;
//         }

//         /// <summary>
//         /// Initiates or retrieves an existing 1:1 chat based on a listing ID.
//         /// Ensures the chat involves the current user and the ad seller.
//         /// </summary>
//         public override async Task<ChatInfo> GetOrCreateChat(GetOrCreateChatRequest request, ServerCallContext context)
//         {
//             if (request.Context == null || request.Context.ListingId <= 0) // Assuming ListingId is int; adjust if string Guid etc.
//             {
//                 throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid ListingId must be provided in the context."));
//             }

//             var listingIdString = request.Context.ListingId.ToString(); // Convert int ListingId to string for Ad Service Client
//             var currentUserId = GetCurrentUserId(); // Buyer's B2C OID

//             _logger.LogInformation("GetOrCreateChat called for ListingId: {ListingId} by User: {UserId}", listingIdString, currentUserId);

//             try
//             {
//                 // 1. Get Ad details to find the seller
//                 var adDetails = await _standardAdsClient.GetAdByIdAsync(listingIdString, context.CancellationToken);
//                 if (adDetails == null)
//                 {
//                     throw new RpcException(new Status(StatusCode.NotFound, $"Ad with ListingId '{listingIdString}' not found."));
//                 }

//                 // Check if the current user is the seller - cannot chat with oneself about their own ad
//                 if (adDetails.SellerId.ToString() == currentUserId) // Assuming SellerId in Ad is the Auth *internal* ID. We need the OID.
//                 {
//                     // This comparison needs adjustment. SellerId is int, currentUserId is string (B2C OID)
//                     // We need to fetch the Seller's profile first.
//                     // throw new RpcException(new Status(StatusCode.InvalidArgument, "Cannot initiate a chat about your own listing."));
//                      _logger.LogWarning("Attempted to initiate chat for own listing {ListingId} by user {UserId}", listingIdString, currentUserId);
//                      // Decide if this should be an error or handled differently. Returning an error seems reasonable.
//                      throw new RpcException(new Status(StatusCode.InvalidArgument, "Cannot initiate a chat about your own listing."));

//                 }

//                 // 2. Get Seller's User Profile from Auth Service using the internal SellerId from the Ad
//                 var sellerProfileRequest = new GetUserProfileRequest { Id = adDetails.SellerId };
//                 var sellerProfileProto = await _authProfileClient.GetUserProfileAsync(sellerProfileRequest, context.CancellationToken);

//                 if (sellerProfileProto == null || string.IsNullOrEmpty(sellerProfileProto.Email)) // Check a required field like email or phone to guess OID presence indirectly
//                 {
//                      // It seems GetUserProfile doesn't return the OID directly based on the proto.
//                      // This is a PROBLEM. We NEED the seller's B2C OID to create the ChatParticipant.
//                      // Possible Solutions:
//                      //    a) Modify Auth Service: GetUserProfileResponse should include the B2C OID (e.g., add a string field `b2c_object_id`). **<- PREFERRED**
//                      //    b) Modify Auth Service: Add a new RPC `GetUserB2cOid(GetUserProfileRequest)` that returns just the OID.
//                      //    c) Workaround (less ideal): Use Seller's Email/Phone from sellerProfileProto to call GetOrCreateAccount? This assumes email/phone are unique identifiers used for linking B2C.
//                      _logger.LogError("Could not retrieve Seller's B2C OID for internal SellerId {SellerId} from Auth Service. Cannot create chat.", adDetails.SellerId);
//                      throw new RpcException(new Status(StatusCode.Internal, "Failed to retrieve necessary seller information to initiate chat. Auth service might need update to provide B2C OID."));
//                 }

//                  // **** TEMPORARY ASSUMPTION/PLACEHOLDER ****
//                  // Assume sellerProfileProto *does* have the OID, maybe in a field named 'B2cObjectId' (adjust field name).
//                  // string sellerB2cOid = sellerProfileProto.B2cObjectId;
//                  // If not, you MUST implement one of the solutions above.
//                  // Let's proceed *assuming* we got the seller's OID somehow, maybe via email lookup as a fallback demo:
//                  _logger.LogWarning("Attempting fallback: Using Seller Email ({SellerEmail}) to find B2C OID via GetOrCreateAccount.", sellerProfileProto.Email);
//                  string sellerB2cOid;
//                  try {
//                     var findSellerReq = new GetOrCreateAccountRequest { Email = sellerProfileProto.Email, PhoneNumber = sellerProfileProto.PhoneNumber ?? "" }; // Need accurate data
//                     var findSellerResp = await _authProfileClient.GetOrCreateAccountAsync(findSellerReq, context.CancellationToken);
//                     // We need the OID from the UserProfile within findSellerResp
//                     // Again, the UserProfile proto doesn't explicitly show the OID. This design needs fixing in Auth service.
//                     // Let's *pretend* the ID returned here IS the OID for the demo. THIS IS WRONG.
//                     // sellerB2cOid = findSellerResp.UserProfile.Id.ToString(); // WRONG MAPPING - ID is internal ID
//                     // You MUST fix the Auth service to return the B2C OID.
//                     // For now, using a placeholder.
//                      sellerB2cOid = $"seller-oid-for-internal-{adDetails.SellerId}"; // <<<<<---- Placeholder - MUST BE FIXED
//                      _logger.LogInformation("Retrieved Seller B2C OID (Placeholder): {SellerOid} for internal ID {InternalSellerId}", sellerB2cOid, adDetails.SellerId);

//                  } catch (Exception findEx) {
//                     _logger.LogError(findEx, "Failed fallback attempt to get seller OID via GetOrCreateAccount using email {SellerEmail}", sellerProfileProto.Email);
//                     throw new RpcException(new Status(StatusCode.Internal, "Failed to resolve seller identity for chat creation."));
//                  }
//                  // **** END TEMPORARY ASSUMPTION ****

//                  // Now we have buyer OID (currentUserId) and seller OID (sellerB2cOid)

//                 // 3. Check if chat already exists between these two users for this listing
//                 var existingChat = await _dbContext.Chats
//                     .Include(c => c.Participants)
//                     .Where(c => c.ListingId == listingIdString)
//                     // Check if the chat has exactly two participants AND both required IDs are present
//                     .Where(c => c.Participants.Count == 2 &&
//                                 c.Participants.Any(p => p.UserId == currentUserId) &&
//                                 c.Participants.Any(p => p.UserId == sellerB2cOid))
//                     .Select(c => new { c.ChatId, ParticipantUserIds = c.Participants.Select(p => p.UserId).ToList() }) // Select only needed data
//                     .FirstOrDefaultAsync(context.CancellationToken);

//                 if (existingChat != null)
//                 {
//                     _logger.LogInformation("Found existing ChatId: {ChatId} for ListingId: {ListingId}", existingChat.ChatId, listingIdString);
//                     return new ChatInfo
//                     {
//                         ChatId = existingChat.ChatId.ToString(),
//                         Context = request.Context, // Return original context
//                         Participants = { existingChat.ParticipantUserIds.Select(id => new UserIdentifier { UserId = ParseUserIdToInt(id) }) } // Map string OIDs back to UserIdentifier (int user_id) -> This mapping is likely incorrect based on proto! user_id should be int.
//                         // The UserIdentifier proto uses int32 user_id. This seems inconsistent with using B2C OID (string) everywhere else.
//                         // Let's assume UserIdentifier.user_id *should* have been string b2c_oid = 1;
//                         // Reverting to placeholder mapping. Fix the proto or the logic.
//                         // Participants = { new UserIdentifier { UserId = 0 /* Map buyer OID? */ }, new UserIdentifier { UserId = 0 /* Map seller OID? */ } } // Placeholder!
//                     };
//                 }

//                 // 4. Create new chat if not found
//                 _logger.LogInformation("Creating new chat for ListingId: {ListingId} between Buyer: {BuyerId} and Seller: {SellerId}", listingIdString, currentUserId, sellerB2cOid);

//                 var newChat = new Chat
//                 {
//                     ListingId = listingIdString,
//                     CreatedAt = DateTime.UtcNow
//                 };

//                 var buyerParticipant = new ChatParticipant
//                 {
//                     Chat = newChat,
//                     UserId = currentUserId, // Buyer's B2C OID
//                     JoinedAt = DateTime.UtcNow
//                 };

//                 var sellerParticipant = new ChatParticipant
//                 {
//                     Chat = newChat,
//                     UserId = sellerB2cOid, // Seller's B2C OID (fetched via Auth)
//                     JoinedAt = DateTime.UtcNow
//                 };

//                 _dbContext.Chats.Add(newChat);
//                 _dbContext.ChatParticipants.AddRange(buyerParticipant, sellerParticipant);

//                 await _dbContext.SaveChangesAsync(context.CancellationToken);
//                 _logger.LogInformation("Created new ChatId: {ChatId}", newChat.ChatId);

//                 // Map ChatParticipants back to UserIdentifiers (assuming UserIdentifier.user_id maps to B2C OID string)
//                 // Again, the proto UserIdentifier uses int32 user_id. This mapping needs clarification.
//                 var participantsProto = new List<UserIdentifier> {
//                     // new UserIdentifier { UserId = ParseUserIdToInt(buyerParticipant.UserId) }, // Placeholder conversion
//                     // new UserIdentifier { UserId = ParseUserIdToInt(sellerParticipant.UserId) }  // Placeholder conversion
//                 };


//                 return new ChatInfo
//                 {
//                     ChatId = newChat.ChatId.ToString(),
//                     Context = request.Context,
//                     Participants = { participantsProto } // Add the correctly mapped participants
//                 };
//             }
//             catch (RpcException) // Re-throw specific gRPC errors
//             {
//                 throw;
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Error in GetOrCreateChat for ListingId: {ListingId}", listingIdString);
//                 throw new RpcException(new Status(StatusCode.Internal, $"An internal error occurred: {ex.Message}"));
//             }
//         }

//         // Helper to attempt parsing B2C OID (string) to int (likely wrong based on proto)
//         private int ParseUserIdToInt(string userId) {
//              _logger.LogWarning("Parsing UserId ('{UserId}') to int for UserIdentifier proto - this mapping is likely incorrect and needs review based on Auth service's ID strategy vs B2C OID.", userId);
//              // Attempt a basic parse or return 0/hash code as placeholder.
//              if (int.TryParse(userId, out int result)) return result;
//              // Maybe use hash code if it must be an int? Highly discouraged.
//              // return userId.GetHashCode();
//              return 0; // Indicate failure or placeholder
//         }


//         /// <summary>
//         /// Sends a message to a specified chat. Validates participation and broadcasts to recipients.
//         /// </summary>
//         public override async Task<Message> SendMessage(SendMessageRequest request, ServerCallContext context)
//         {
//             if (!Guid.TryParse(request.ChatId, out var chatIdGuid))
//             {
//                 throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ChatId format. Must be a GUID."));
//             }
//             if (string.IsNullOrEmpty(request.Content) && request.MessageType == MessageType.Text)
//             {
//                  throw new RpcException(new Status(StatusCode.InvalidArgument, "Content cannot be empty for TEXT messages."));
//             }
//              if (request.MessageType == MessageType.Media && (request.MediaInfo == null || string.IsNullOrEmpty(request.MediaInfo.MediaUrl)))
//             {
//                  throw new RpcException(new Status(StatusCode.InvalidArgument, "MediaInfo with MediaUrl must be provided for MEDIA messages."));
//             }


//             var currentUserId = GetCurrentUserId();
//             _logger.LogInformation("SendMessage called for ChatId: {ChatId} by User: {UserId}", request.ChatId, currentUserId);

//             try
//             {
//                 // 1. Validate participant
//                 var isParticipant = await _dbContext.ChatParticipants
//                     .AnyAsync(cp => cp.ChatId == chatIdGuid && cp.UserId == currentUserId, context.CancellationToken);

//                 if (!isParticipant)
//                 {
//                     _logger.LogWarning("User {UserId} attempted to send message to ChatId {ChatId} but is not a participant.", currentUserId, request.ChatId);
//                     throw new RpcException(new Status(StatusCode.PermissionDenied, "You are not a participant in this chat."));
//                 }

//                 // 2. Create and Save the Message
//                 var dbMessage = new ChatMessage
//                 {
//                     ChatId = chatIdGuid,
//                     SenderUserId = currentUserId, // B2C OID
//                     SenderActingAsBusinessId = request.ActingAsBusinessId, // Optional Business ID
//                     Content = request.Content,
//                     MessageType = MapProtoMessageTypeToDb(request.MessageType), // Map enum
//                     Timestamp = DateTime.UtcNow,
//                     MediaUrl = request.MediaInfo?.MediaUrl,
//                     MediaFileName = request.MediaInfo?.FileName,
//                     MediaMimeType = request.MediaInfo?.MimeType,
//                     MediaSizeBytes = request.MediaInfo?.SizeBytes
//                 };

//                 _dbContext.Messages.Add(dbMessage);
//                 await _dbContext.SaveChangesAsync(context.CancellationToken);

//                 _logger.LogInformation("Message {MessageId} saved for ChatId {ChatId}", dbMessage.MessageId, request.ChatId);

//                 // 3. Map DB message to Proto message
//                 var protoMessage = MapDbMessageToProto(dbMessage);

//                 // 4. Broadcast to other participants via ConnectionManager
//                 var recipientIds = await _dbContext.ChatParticipants
//                     .Where(cp => cp.ChatId == chatIdGuid /*&& cp.UserId != currentUserId*/) // Broadcast to ALL participants including sender for consistency? Or exclude sender? Let's include sender for now.
//                     .Select(cp => cp.UserId)
//                     .ToListAsync(context.CancellationToken);

//                 if (recipientIds.Any())
//                 {
//                     _logger.LogDebug("Broadcasting message {MessageId} to recipients: {RecipientIds}", protoMessage.MessageId, string.Join(",", recipientIds));
//                     await _connectionManager.BroadcastMessageAsync(protoMessage, recipientIds);
//                 }

//                 // 5. Return the sent message confirmation
//                 return protoMessage;

//             }
//             catch (RpcException)
//             {
//                 throw;
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Error in SendMessage for ChatId: {ChatId}", request.ChatId);
//                 throw new RpcException(new Status(StatusCode.Internal, $"An internal error occurred while sending message: {ex.Message}"));
//             }
//         }

//         /// <summary>
//         /// Establishes a server stream for a client to receive real-time messages.
//         /// </summary>
//         public override async Task SubscribeToMessages(SubscribeRequest request, IServerStreamWriter<Message> responseStream, ServerCallContext context)
//         {
//             var userId = GetCurrentUserId();
//             _logger.LogInformation("User {UserId} subscribing to messages.", userId);

//             _connectionManager.Subscribe(userId, responseStream);

//             try
//             {
//                 // Keep the connection open until the client disconnects or cancellation is requested
//                 await context.CancellationToken.WaitHandle.WaitOneAsync(context.CancellationToken);
//                  _logger.LogDebug("WaitHandle released for user {UserId}.", userId);

//             }
//             catch (OperationCanceledException) {
//                  _logger.LogInformation("Subscription cancelled for User {UserId}.", userId);
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Error during message subscription for User {UserId}.", userId);
//                 // Optionally re-throw or just ensure cleanup happens
//             }
//             finally
//             {
//                 _connectionManager.Unsubscribe(userId);
//                 _logger.LogInformation("User {UserId} unsubscribed from messages.", userId);
//             }
//         }

//         /// <summary>
//         /// Retrieves historical messages for a chat with pagination.
//         /// </summary>
//         public override async Task<GetChatHistoryResponse> GetChatHistory(GetChatHistoryRequest request, ServerCallContext context)
//         {
//             if (!Guid.TryParse(request.ChatId, out var chatIdGuid))
//             {
//                 throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ChatId format. Must be a GUID."));
//             }

//             var currentUserId = GetCurrentUserId();
//             int pageSize = request.PageSize > 0 && request.PageSize <= 100 ? request.PageSize : 50; // Default and max page size

//             _logger.LogInformation("GetChatHistory called for ChatId: {ChatId} by User: {UserId}, PageSize: {PageSize}, BeforeMessageId: {BeforeMessageId}",
//                 request.ChatId, currentUserId, pageSize, request.BeforeMessageId);

//             try
//             {
//                 // 1. Validate participant
//                 var isParticipant = await _dbContext.ChatParticipants
//                     .AnyAsync(cp => cp.ChatId == chatIdGuid && cp.UserId == currentUserId, context.CancellationToken);

//                 if (!isParticipant)
//                 {
//                     _logger.LogWarning("User {UserId} attempted to get history for ChatId {ChatId} but is not a participant.", currentUserId, request.ChatId);
//                     throw new RpcException(new Status(StatusCode.PermissionDenied, "You are not a participant in this chat."));
//                 }

//                 // 2. Build Query
//                 var query = _dbContext.Messages
//                     .Where(m => m.ChatId == chatIdGuid)
//                     .OrderByDescending(m => m.Timestamp) // Newest first
//                     .ThenByDescending(m => m.MessageId); // Secondary sort for consistent pagination if timestamps are identical

//                 // 3. Apply Pagination (Cursor-based)
//                 if (!string.IsNullOrEmpty(request.BeforeMessageId) && Guid.TryParse(request.BeforeMessageId, out var beforeMessageGuid))
//                 {
//                     // Find the timestamp of the cursor message
//                     var cursorTimestamp = await _dbContext.Messages
//                         .Where(m => m.MessageId == beforeMessageGuid)
//                         .Select(m => m.Timestamp)
//                         .FirstOrDefaultAsync(context.CancellationToken);

//                     if (cursorTimestamp != default)
//                     {
//                          _logger.LogDebug("Paginating history for ChatId {ChatId} before Timestamp: {CursorTimestamp}", request.ChatId, cursorTimestamp);
//                         query = query.Where(m => m.Timestamp < cursorTimestamp); // Get messages older than the cursor
//                     } else {
//                          _logger.LogWarning("BeforeMessageId {BeforeMessageId} not found for ChatId {ChatId}. Ignoring cursor.", request.BeforeMessageId, request.ChatId);
//                     }
//                 }

//                 // Fetch one extra message to determine if there are more pages
//                 var dbMessages = await query
//                     .Take(pageSize + 1)
//                     .ToListAsync(context.CancellationToken);

//                 // 4. Determine if more messages exist
//                 bool hasMoreMessages = dbMessages.Count > pageSize;
//                 var messagesToSend = dbMessages.Take(pageSize).ToList(); // Get only the requested page size

//                  _logger.LogDebug("Retrieved {MessageCount} messages for ChatId {ChatId}. HasMoreMessages: {HasMore}", messagesToSend.Count, request.ChatId, hasMoreMessages);


//                 // 5. Map to Proto response
//                 var response = new GetChatHistoryResponse
//                 {
//                     HasMoreMessages = hasMoreMessages
//                 };
//                 response.Messages.AddRange(messagesToSend.Select(MapDbMessageToProto)); // Map each message

//                 return response;
//             }
//             catch (RpcException)
//             {
//                 throw;
//             }
//             catch (Exception ex)
//             {
//                  _logger.LogError(ex, "Error in GetChatHistory for ChatId: {ChatId}", request.ChatId);
//                 throw new RpcException(new Status(StatusCode.Internal, $"An internal error occurred while getting chat history: {ex.Message}"));
//             }
//         }

//         /// <summary>
//         /// Generates a SAS URL for uploading media to Azure Blob Storage.
//         /// </summary>
//         public override async Task<GetMediaUploadUrlResponse> GetMediaUploadUrl(GetMediaUploadUrlRequest request, ServerCallContext context)
//         {
//              if (!Guid.TryParse(request.ChatId, out var chatIdGuid))
//             {
//                 throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ChatId format. Must be a GUID."));
//             }
//             if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.MimeType))
//             {
//                 throw new RpcException(new Status(StatusCode.InvalidArgument, "FileName and MimeType are required."));
//             }

//             var currentUserId = GetCurrentUserId();
//              _logger.LogInformation("GetMediaUploadUrl called for ChatId: {ChatId} by User: {UserId}, FileName: {FileName}",
//                 request.ChatId, currentUserId, request.FileName);

//             try
//             {
//                 // 1. Validate participant (optional but good practice)
//                  var isParticipant = await _dbContext.ChatParticipants
//                     .AnyAsync(cp => cp.ChatId == chatIdGuid && cp.UserId == currentUserId, context.CancellationToken);
//                  if (!isParticipant) {
//                      _logger.LogWarning("User {UserId} attempted to get upload URL for ChatId {ChatId} but is not a participant.", currentUserId, request.ChatId);
//                      throw new RpcException(new Status(StatusCode.PermissionDenied, "You are not a participant in this chat."));
//                  }


//                 // 2. Generate Blob Name (ensure uniqueness and organization)
//                 var uniqueFileName = $"{Guid.NewGuid()}-{request.FileName}";
//                 // Structure: chat/{chatId}/user/{userId}/{uniqueFileName}
//                 var blobName = $"chat/{request.ChatId}/user/{currentUserId}/{uniqueFileName}";

//                 // 3. Get SAS URI from Blob Service
//                 var expiration = TimeSpan.FromMinutes(15); // Make SAS URL short-lived
//                 var sasUri = await _blobStorageService.GetUploadSasUriAsync(blobName, expiration);

//                 // 4. Get the final permanent URL for the blob (to be stored in message)
//                 var permanentMediaUrl = _blobStorageService.GetDownloadUrl(blobName);

//                 _logger.LogDebug("Generated SAS Upload URL for Blob: {BlobName}", blobName);

//                 return new GetMediaUploadUrlResponse
//                 {
//                     UploadUrl = sasUri.ToString(),
//                     BlobName = blobName,
//                     MediaUrl = permanentMediaUrl
//                 };
//             }
//              catch (RpcException)
//             {
//                 throw;
//             }
//             catch (InvalidOperationException ioex) // Catch specific errors from BlobStorageService
//             {
//                  _logger.LogError(ioex, "Configuration or permission error generating SAS URL for ChatId: {ChatId}", request.ChatId);
//                 throw new RpcException(new Status(StatusCode.FailedPrecondition, $"Failed to generate upload URL: {ioex.Message}"));
//             }
//             catch (Exception ex)
//             {
//                  _logger.LogError(ex, "Error in GetMediaUploadUrl for ChatId: {ChatId}", request.ChatId);
//                 throw new RpcException(new Status(StatusCode.Internal, $"An internal error occurred while getting upload URL: {ex.Message}"));
//             }
//         }

//         /// <summary>
//         /// Allows authorized internal services to inject system messages into a chat.
//         /// NOTE: Authorization needs careful consideration here. Currently allows any authenticated user.
//         /// Consider adding a specific policy or role requirement if exposed externally,
//         /// or rely on internal network trust if called service-to-service.
//         /// </summary>
//         // [Authorize(Policy = "InternalServiceOnly")] // Example: Add a specific policy if needed
//         public override async Task<Empty> InjectSystemMessage(InjectSystemMessageRequest request, ServerCallContext context)
//         {
//             if (!Guid.TryParse(request.ChatId, out var chatIdGuid))
//             {
//                 throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ChatId format. Must be a GUID."));
//             }
//             if (string.IsNullOrWhiteSpace(request.Content))
//             {
//                  throw new RpcException(new Status(StatusCode.InvalidArgument, "Content cannot be empty for system messages."));
//             }
//              // Validate MessageType is appropriate for system injection
//              if (request.MessageType != MessageType.System && request.MessageType != MessageType.CallLog)
//             {
//                 throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid MessageType for system injection. Must be SYSTEM or CALL_LOG."));
//             }

//             _logger.LogInformation("InjectSystemMessage called for ChatId: {ChatId}, Type: {MessageType}", request.ChatId, request.MessageType);
//             // TODO: Add robust authorization check here if necessary.

//             try
//             {
//                 // 1. Find the chat (to ensure it exists) and its participants
//                  var chatExists = await _dbContext.Chats.AnyAsync(c => c.ChatId == chatIdGuid, context.CancellationToken);
//                  if (!chatExists) {
//                      _logger.LogWarning("InjectSystemMessage failed: ChatId {ChatId} not found.", request.ChatId);
//                      throw new RpcException(new Status(StatusCode.NotFound, $"Chat with ID '{request.ChatId}' not found."));
//                  }


//                 // 2. Create and Save the System Message
//                  const string systemSenderId = "SYSTEM"; // Define a constant identifier for system messages
//                  var dbMessage = new ChatMessage
//                  {
//                     ChatId = chatIdGuid,
//                     SenderUserId = systemSenderId, // Use system identifier
//                     Content = request.Content,
//                     MessageType = MapProtoMessageTypeToDb(request.MessageType),
//                     Timestamp = DateTime.UtcNow
//                  };

//                 _dbContext.Messages.Add(dbMessage);
//                 await _dbContext.SaveChangesAsync(context.CancellationToken);

//                  _logger.LogInformation("System message {MessageId} injected into ChatId {ChatId}", dbMessage.MessageId, request.ChatId);

//                 // 3. Map and Broadcast
//                 var protoMessage = MapDbMessageToProto(dbMessage);

//                 var recipientIds = await _dbContext.ChatParticipants
//                     .Where(cp => cp.ChatId == chatIdGuid)
//                     .Select(cp => cp.UserId)
//                     .ToListAsync(context.CancellationToken);

//                  if (recipientIds.Any())
//                  {
//                      _logger.LogDebug("Broadcasting system message {MessageId} to recipients: {RecipientIds}", protoMessage.MessageId, string.Join(",", recipientIds));
//                     await _connectionManager.BroadcastMessageAsync(protoMessage, recipientIds);
//                  }

//                  return new Empty();
//             }
//             catch (RpcException)
//             {
//                 throw;
//             }
//             catch (Exception ex)
//             {
//                  _logger.LogError(ex, "Error in InjectSystemMessage for ChatId: {ChatId}", request.ChatId);
//                 throw new RpcException(new Status(StatusCode.Internal, $"An internal error occurred while injecting system message: {ex.Message}"));
//             }
//         }

//         // --- Helper Mappers ---

//         private Message MapDbMessageToProto(ChatMessage dbMessage)
//         {
//             if (dbMessage == null) return null;

//             var protoMessage = new Message
//             {
//                 MessageId = dbMessage.MessageId.ToString(),
//                 ChatId = dbMessage.ChatId.ToString(),
//                 Content = dbMessage.Content ?? "",
//                 Timestamp = Timestamp.FromDateTime(dbMessage.Timestamp.ToUniversalTime()),
//                 MessageType = MapDbMessageTypeToProto(dbMessage.MessageType),
//                 SenderInfo = new SenderInfo
//                 {
//                     // User = new UserIdentifier { UserId = ParseUserIdToInt(dbMessage.SenderUserId) }, // Map B2C OID (string) to UserIdentifier (int) -> Needs review!
//                     User = new UserIdentifier { UserId = 0 }, // Placeholder - Fix UserIdentifier proto or logic
//                     ActingAsBusinessId = dbMessage.SenderActingAsBusinessId ?? "" // Handle null
//                 }
//             };
//              // Set SenderInfo.User correctly based on how UserIdentifier should store the ID (int or string OID)

//             if (dbMessage.MessageType == DbMessageType.MEDIA)
//             {
//                 protoMessage.MediaInfo = new MediaInfo
//                 {
//                     MediaUrl = dbMessage.MediaUrl ?? "",
//                     FileName = dbMessage.MediaFileName ?? "",
//                     MimeType = dbMessage.MediaMimeType ?? "",
//                     SizeBytes = dbMessage.MediaSizeBytes ?? 0
//                 };
//             }

//             return protoMessage;
//         }

//         private DbMessageType MapProtoMessageTypeToDb(MessageType protoType)
//         {
//             return protoType switch
//             {
//                 MessageType.Text => DbMessageType.TEXT,
//                 MessageType.Media => DbMessageType.MEDIA,
//                 MessageType.System => DbMessageType.SYSTEM,
//                 MessageType.CallLog => DbMessageType.CALL_LOG,
//                 _ => throw new ArgumentOutOfRangeException(nameof(protoType), $"Unsupported message type: {protoType}")
//             };
//         }

//         private MessageType MapDbMessageTypeToProto(DbMessageType dbType)
//         {
//              return dbType switch
//             {
//                 DbMessageType.TEXT => MessageType.Text,
//                 DbMessageType.MEDIA => MessageType.Media,
//                 DbMessageType.SYSTEM => MessageType.System,
//                 DbMessageType.CALL_LOG => MessageType.CallLog,
//                 _ => MessageType.Text // Or throw? Defaulting to Text might hide errors.
//             };
//         }
//     }

//      // Helper Extension for async WaitHandle waiting with CancellationToken
//     internal static class WaitHandleExtensions
//     {
//         public static Task WaitOneAsync(this WaitHandle waitHandle, CancellationToken cancellationToken)
//         {
//             if (waitHandle == null)
//                 throw new ArgumentNullException(nameof(waitHandle));

//             var tcs = new TaskCompletionSource<bool>();

//             var registration = ThreadPool.RegisterWaitForSingleObject(
//                 waitHandle,
//                 (state, timedOut) =>
//                 {
//                     var localTcs = (TaskCompletionSource<bool>)state;
//                     if (timedOut) {
//                          localTcs.TrySetCanceled(); // Consider setting Canceled if timeout occurs (though WaitOne doesn't naturally timeout here)
//                     } else {
//                         localTcs.TrySetResult(true);
//                     }

//                 },
//                 tcs,
//                 Timeout.InfiniteTimeSpan, // Wait indefinitely until signaled or cancelled
//                 true); // Execute callback once

//             // Clean up registration when task completes or is cancelled
//             tcs.Task.ContinueWith((_, state) => ((RegisteredWaitHandle)state).Unregister(null), registration, TaskScheduler.Default);


//              // Handle cancellation from the ServerCallContext
//             if (cancellationToken.CanBeCanceled) {
//                  var cancelReg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
//                  tcs.Task.ContinueWith((_, state) => ((CancellationTokenRegistration)state).Dispose(), cancelReg, TaskScheduler.Default);
//             }


//             return tcs.Task;
//         }
//     }
// }