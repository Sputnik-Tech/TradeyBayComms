namespace MessagingService.ExternalDTOs
{
    public class StandardAdDTO
    {
        public Guid Id { get; set; }
        public List<string> ImageUrls { get; set; } = new(); // Up to 10 images, watermark applied to each
        public required string AdName { get; set; }
        public decimal AdPrice { get; set; }
        public required string AdDescription { get; set; }
        public required int SellerId { get; set; } // Retrieved from Auth Microservice
        public string SellerFullName { get; set; } = string.Empty; // Seller full name (e.g., Name + Surname)
        public double? SellerRating { get; set; }
        public string SellerEmail { get; set; } = string.Empty;
        public string SellerPhone { get; set; } = string.Empty;
        // New property for the seller type.
        public string SellerType { get; set; } = "User";  // or "Business"

        public bool HasDiscount { get; set; }
        
        public bool IsPremium { get; set; }
        public DateTime PublishedDate { get; set; }
        public int ViewsCount { get; set; }
        public bool IsLiked { get; set; }
        public bool IsViewed { get; set; }
        public List<PriceSuggestionDTO> PriceSuggestions { get; set; } = new();
        public string Status { get; set; } = "Active"; // Status of the ad (Active, Archived, Sold, Removed)
        public string? BuyerId { get; set; } // ID of the buyer if the ad is sold
        public required int CategoryId { get; set; } // Retrieved from Category Microservice
        public required int SubCategoryId { get; set; } // Retrieved from Category Microservice
        // New properties for location
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? FormattedAddress { get; set; } = string.Empty;
    }

    public class PriceSuggestionDTO
    {
        public Guid Id { get; set; } // Unique identifier for each suggestion
        public required int BuyerId { get; set; } // Retrieved from Auth Microservice
        public decimal SuggestedPrice { get; set; }
        public DateTime SuggestedAt { get; set; }
        public bool IsAccepted { get; set; }

        // New properties to store buyer details:
        public string BuyerFullName { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
        public string BuyerPhone { get; set; } = string.Empty;
    }
}
