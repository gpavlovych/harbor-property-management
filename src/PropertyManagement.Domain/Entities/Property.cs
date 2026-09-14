namespace PropertyManagement.Domain.Entities;

public class Property
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<Unit> Units { get; set; } = new List<Unit>();

    public string FullAddress => $"{StreetAddress}, {City}, {State} {PostalCode}";
}
