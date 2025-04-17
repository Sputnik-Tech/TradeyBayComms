using System;
using System.Threading.Tasks;
using TradeyBay.Auth; // Proto types
using Microsoft.Extensions.Logging;
using MessagingService.Interfaces;
using Grpc.Core;

namespace MessagingService.GrpcClients
{
    public class AuthBusinessGrpcClient : IAuthBusinessGrpcClient
    {
        private readonly BusinessService.BusinessServiceClient _client;
        private readonly ILogger<AuthBusinessGrpcClient> _logger;

        public AuthBusinessGrpcClient(BusinessService.BusinessServiceClient client, ILogger<AuthBusinessGrpcClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        // --- ADD THIS METHOD IMPLEMENTATION ---
        public async Task<GetBusinessesByUserResponse> GetBusinessesByUserAsync(GetBusinessesByUserRequest request)
        {
            _logger.LogDebug("Calling GetBusinessesByUserAsync for UserProfileId: {UserProfileId}", request.UserProfileId);
            try
            {
                // Call the gRPC method on the underlying client
                var response = await _client.GetBusinessesByUserAsync(request);
                _logger.LogDebug("Successfully received {Count} businesses for UserProfileId: {UserProfileId}", response?.Businesses?.Count ?? 0, request.UserProfileId);
                return response;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RpcException occurred while calling GetBusinessesByUserAsync for UserProfileId: {UserProfileId}. Status: {StatusCode}", request.UserProfileId, ex.StatusCode);
                // Re-throw or handle specific statuses if needed (e.g., NotFound might not be an error)
                throw;
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Unexpected exception occurred while calling GetBusinessesByUserAsync for UserProfileId: {UserProfileId}", request.UserProfileId);
                 throw; // Re-throw general exceptions
            }
        }
        // --- END ADDED METHOD IMPLEMENTATION ---

        public async Task<RegisterBusinessResponse> RegisterBusinessAsync(RegisterBusinessRequest request)
        {
            return await _client.RegisterBusinessAsync(request);
        }

        public async Task<GetBusinessResponse> GetBusinessAsync(GetBusinessRequest request)
        {
            return await _client.GetBusinessAsync(request);
        }

        public async Task<UpdateBusinessResponse> UpdateBusinessAsync(UpdateBusinessRequest request)
        {
            return await _client.UpdateBusinessAsync(request);
        }

        public async Task<DeleteBusinessResponse> DeleteBusinessAsync(DeleteBusinessRequest request)
        {
            return await _client.DeleteBusinessAsync(request);
        }

        public async Task<UpdateBusinessLocationResponse> UpdateBusinessLocationAsync(UpdateBusinessLocationRequest request)
        {
            return await _client.UpdateBusinessLocationAsync(request);
        }
    }
}
