using Web2_ThueNha.Models.Identity;

namespace Web2_ThueNha.Models.Properties;

public class Property
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long SequenceNumber { get; set; }
    public Guid LandlordId { get; set; }
    public int PropertyTypeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerMonth { get; set; }
    public decimal DepositAmount { get; set; }
    public int MaxOccupants { get; set; } = 2;
    public string? FurnishingDescription { get; set; }
    public string? AdditionalFeesDescription { get; set; }
    public decimal Area { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public string? WardName { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public PropertyModerationStatus ModerationStatus { get; set; } = PropertyModerationStatus.Pending;
    public PropertyListingStatus ListingStatus { get; set; } = PropertyListingStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User Landlord { get; set; } = null!;
    public PropertyType PropertyType { get; set; } = null!;
    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<PropertyAmenity> PropertyAmenities { get; set; } = new List<PropertyAmenity>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
