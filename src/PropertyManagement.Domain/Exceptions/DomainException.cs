namespace PropertyManagement.Domain.Exceptions;

/// <summary>A business rule was violated. The message is safe to show to the user.</summary>
public class DomainException(string message, string? memberName = null) : Exception(message)
{
    /// <summary>Optional input member the error belongs to, so the presentation layer can attach it to the right field.</summary>
    public string? MemberName { get; } = memberName;
}
