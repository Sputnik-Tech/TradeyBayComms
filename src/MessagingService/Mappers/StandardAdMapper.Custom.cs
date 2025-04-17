// StandardAdMapper.Custom.cs (In MessagingService project)
using Google.Protobuf.WellKnownTypes;
using System;

namespace MessagingService.Mappers
{
    // Must be declared as partial to extend the Mapperly-generated class
    public partial class StandardAdMapper
    {
        // Explicit implementation using manual epoch calculation,
        // based on the logic from your StandardAdsService example.
        // This avoids the .ToDateTime() extension method call.
        private DateTime MapTimestampToDateTime(Timestamp? timestamp)
        {
            if (timestamp == null)
            {
                // Consistent default value (UnixEpoch UTC) if timestamp is null
                // and DTO requires non-nullable DateTime.
                // Reconsider if DateTime? is more appropriate for the DTO.
                // Console.WriteLine("Warning: Null Timestamp encountered, returning UnixEpoch.");
                return DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc);
            }

            // Manual conversion from Timestamp seconds/nanos
            try
            {
                // Protect against potential out-of-range values if necessary,
                // although values from valid Timestamps should be fine.
                var dateTime = DateTime.UnixEpoch.AddSeconds(timestamp.Seconds).AddTicks(timestamp.Nanos / 100);
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc); // Ensure kind is UTC
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // Handle cases where timestamp values might be invalid for DateTime
                // Log error: Timestamp value resulted in out-of-range DateTime
                Console.WriteLine($"Error converting Timestamp sec={timestamp.Seconds}, nanos={timestamp.Nanos}: {ex.Message}");
                // Return a default value or rethrow depending on requirements
                return DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc);
            }
        }
    }
}