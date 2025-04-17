using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using TradeyBay.Auth;
using MessagingService.ExternalDTOs;
using MessagingService.Mappers;
using MessagingService.Interfaces;

namespace MessagingService.GrpcClients
{
    public class AuthUserProfileGrpcClient : IAuthUserProfileGrpcClient
    {
        private readonly UserProfileService.UserProfileServiceClient _client;
        private readonly AuthUserProfileMapper _mapper;

        public AuthUserProfileGrpcClient(UserProfileService.UserProfileServiceClient client, AuthUserProfileMapper mapper)
        {
            _client = client;
            _mapper = mapper;
        }

        public async Task<UserProfileDTO> GetUserProfileAsync(GetUserProfileRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var protoResponse = await _client.GetUserProfileAsync(request, cancellationToken: cancellationToken);
                return _mapper.FromProto(protoResponse);
            }
            catch (RpcException ex)
            {
                // Handle or log error as needed.
                throw;
            }
        }

        public async Task<UserProfileDTO> GetOrCreateAccountAsync(GetOrCreateAccountRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var protoResponse = await _client.GetOrCreateAccountAsync(request, cancellationToken: cancellationToken);
                // Assuming the response contains a UserProfile field.
                return _mapper.FromProto(protoResponse.UserProfile);
            }
            catch (RpcException ex)
            {
                throw;
            }
        }

        public async Task<UserProfileDTO> UpdateUserProfileAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var protoResponse = await _client.UpdateUserProfileAsync(request, cancellationToken: cancellationToken);
                return _mapper.FromProto(protoResponse.UserProfile);
            }
            catch (RpcException ex)
            {
                throw;
            }
        }

        public async Task<UserProfileDTO> UpdateUserLocationAsync(UpdateUserLocationRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var protoResponse = await _client.UpdateUserLocationAsync(request, cancellationToken: cancellationToken);
                return _mapper.FromProto(protoResponse.UserProfile);
            }
            catch (RpcException ex)
            {
                throw;
            }
        }
    }
}
