using MessagingService.ExternalDTOs;
using TradeyBay.Auth; // for request messages

namespace MessagingService.Interfaces
{
    public interface IAuthUserProfileGrpcClient
    {
        Task<UserProfileDTO> GetUserProfileAsync(GetUserProfileRequest request, CancellationToken cancellationToken = default);
        Task<UserProfileDTO> GetOrCreateAccountAsync(GetOrCreateAccountRequest request, CancellationToken cancellationToken = default);
        Task<UserProfileDTO> UpdateUserProfileAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default);
        Task<UserProfileDTO> UpdateUserLocationAsync(UpdateUserLocationRequest request, CancellationToken cancellationToken = default);
    }
}
