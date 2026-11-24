namespace Web2_ThueNha.Models.Properties;

public class PropertyAmenity
{
    public Guid PropertyId { get; set; }
    public int AmenityId { get; set; }
    public Property Property { get; set; } = null!;
    public Amenity Amenity { get; set; } = null!;
}
