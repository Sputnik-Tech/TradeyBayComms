using Riok.Mapperly.Abstractions;
using MessagingService.ExternalDTOs;
using Proto = TradeyBay.Auth; // Proto‑generated types

namespace MessagingService.Mappers
{
    [Mapper]
    public partial class AuthBusinessProfileMapper
    {
        // Converts a BusinessProfileDTO to a proto BusinessProfile.
        public partial Proto.BusinessProfile ToProto(BusinessProfileDTO dto);

        // Converts a proto RegisterBusinessRequest to a BusinessProfileDTO.
        public partial BusinessProfileDTO FromRegisterRequest(Proto.RegisterBusinessRequest request);

        // Updates an existing BusinessProfileDTO using data from a proto BusinessProfile message.
        public partial BusinessProfileDTO UpdateBusinessFromProto(Proto.BusinessProfile proto, BusinessProfileDTO existing);
    }
}
