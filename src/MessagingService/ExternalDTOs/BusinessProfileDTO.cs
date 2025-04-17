using System.ComponentModel.DataAnnotations;

namespace MessagingService.ExternalDTOs
{
    public class BusinessProfileDTO
    {
        
        public int? Id { get; set; }

        // Link back to the user
        public int? UserProfileId { get; set; }

        public string? BusinessName { get; set; }
        public string? BusinessEmailAddress { get; set; }
        public string? BusinessPhoneNumber { get; set; }
        public string? BusinessWebsite { get; set; }
        public string? BusinessAddress { get; set; }
        public string? Country { get; set; }
        public string? BusinessTypeOrCategory { get; set; }
        public string? BusinessTaxId { get; set; }

        // NEW: Add location fields
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? FormattedAddress { get; set; }

        // NEW: Rating fields
        public double? AverageRating { get; set; }
        public int? RatingCount { get; set; }
    }
}
