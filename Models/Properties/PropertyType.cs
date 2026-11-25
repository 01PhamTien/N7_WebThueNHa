namespace Web2_ThueNha.Models.Properties;

public class PropertyType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Property> Properties { get; set; } = new List<Property>();
}
