using Riok.Mapperly.Abstractions;
using MessagingService.ExternalDTOs;
using Proto = TradeyBay.Auth; // Proto‑generated types

namespace MessagingService.Mappers
{
    [Mapper]
    public partial class AuthUserProfileMapper
    {
        // Converts an external UserProfileDTO to a proto UserProfile.
        public partial Proto.UserProfile ToProto(UserProfileDTO dto);

        // Converts a proto UserProfile to an external UserProfileDTO.
        public partial UserProfileDTO FromProto(Proto.UserProfile proto);
    }
}
