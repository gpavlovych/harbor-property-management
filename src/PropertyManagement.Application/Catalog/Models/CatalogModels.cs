using PropertyManagement.Application.Common.Models;

namespace PropertyManagement.Application.Catalog.Models;

public record PropertyInput(int Id, string Name, string StreetAddress, string City, string State, string PostalCode);

public class PropertySummary
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public int UnitCount { get; init; }
    public int AvailableUnitCount { get; init; }
}

public class PropertyDetail
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string FullAddress => $"{StreetAddress}, {City}, {State} {PostalCode}";
}

public record UnitInput(int Id, int PropertyId, string UnitNumber, int Bedrooms, decimal MonthlyRent, int UnitTypeId);

public class UnitDetail
{
    public int Id { get; init; }
    public int PropertyId { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string UnitNumber { get; init; } = string.Empty;
    public int Bedrooms { get; init; }
    public decimal MonthlyRent { get; init; }
    public int UnitTypeId { get; init; }
}

public class UnitRow
{
    public int Id { get; init; }
    public int PropertyId { get; init; }
    public string UnitNumber { get; init; } = string.Empty;
    public string UnitType { get; init; } = string.Empty;
    public bool UnitTypeIsActive { get; init; }
    public int Bedrooms { get; init; }
    public decimal MonthlyRent { get; init; }
    public bool IsAvailable { get; init; }
    public int SubmittedApplicationCount { get; init; }
}

public record UnitBrowseFilter
{
    public int? PropertyId { get; init; }
    public int? UnitTypeId { get; init; }
    public int? MinBedrooms { get; init; }
    public decimal? MaxRent { get; init; }
}

public class UnitCard
{
    public int UnitId { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string UnitNumber { get; init; } = string.Empty;
    public string UnitType { get; init; } = string.Empty;
    public int Bedrooms { get; init; }
    public decimal MonthlyRent { get; init; }
    /// <summary>The current applicant's open application for this unit, if any.</summary>
    public int? OpenApplicationId { get; init; }
}

public class UnitBrowseResult
{
    public UnitBrowseFilter Filter { get; init; } = new();
    public List<UnitCard> Units { get; init; } = [];
    public List<LookupItem> Properties { get; init; } = [];
    public List<LookupItem> UnitTypes { get; init; } = [];
}
