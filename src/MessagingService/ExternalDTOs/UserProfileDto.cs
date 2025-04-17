using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MessagingService.ExternalDTOs
{
    public class UserProfileDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("surname")]
        public string? Surname { get; set; }

        [JsonPropertyName("email")]
        [Required]
        public string Email { get; set; } = null!;

        [JsonPropertyName("phoneNumber")]
        [Required]
        public string PhoneNumber { get; set; } = null!;

        [JsonPropertyName("dateOfBirth")]
        public DateTime? DateOfBirth { get; set; }

        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("countryCode")]
        public int? CountryCode { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("formattedAddress")]
        public string? FormattedAddress { get; set; }
        // Null if the user has never been rated; otherwise the average rating (e.g. 4.3).
        [JsonPropertyName("averageRating")]
        public double? AverageRating { get; set; }

        // Total number of rating records for this user
        [JsonPropertyName("ratingCount")]
        public int RatingCount { get; set; }


        // Full constructor
        public UserProfileDTO(int id, string name, string surname, string email, string phoneNumber,
            DateTime? dateOfBirth, string gender, int? countryCode, double? latitude, double? longitude, string? formattedAddress, double? averageRating, int ratingCount)
        {
            Id = id;
            Name = name;
            Surname = surname;
            Email = email;
            PhoneNumber = phoneNumber;
            DateOfBirth = dateOfBirth;
            Gender = gender;
            CountryCode = countryCode;
            Latitude = latitude;
            Longitude = longitude;
            FormattedAddress = formattedAddress;
            AverageRating = averageRating;
            RatingCount = ratingCount;
        }

        // Simplified constructor for common use cases
        public UserProfileDTO(string email, string phoneNumber)
        {
            Email = email;
            PhoneNumber = phoneNumber;
        }

        // Parameterless constructor for scenarios like serialization
        public UserProfileDTO() { }
    }
}
