namespace PropertyManagement.Application.Common;

public static class Roles
{
    public const string Applicant = "Applicant";
    public const string PropertyManager = "PropertyManager";

    public static readonly string[] All = [Applicant, PropertyManager];
}
