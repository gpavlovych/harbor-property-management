namespace PropertyManagement.Application.Common.Models;

/// <summary>The only view of an identity user the application layer needs.</summary>
public class UserSummary
{
    public string Id { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
}
