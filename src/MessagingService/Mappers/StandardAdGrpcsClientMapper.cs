using Riok.Mapperly.Abstractions;
using MessagingService.ExternalDTOs;
using Google.Protobuf.WellKnownTypes; // For Timestamp
using System;
using System.Collections.Generic; // For List/IEnumerable
using Proto = TradeyBay.StandardAds; // Proto-generated types namespace

namespace MessagingService.Mappers
{
    [Mapper(UseDeepCloning = true)] // Use deep cloning for safety if needed
    public partial class StandardAdMapper // Ensure 'partial' is present
{
    // ... Keep your main mapping methods declared as partial ...
    public partial StandardAdDTO FromProto(Proto.StandardAd proto);
    private partial PriceSuggestionDTO MapPriceSuggestionProtoToDto(Proto.PriceSuggestion proto);

}
}