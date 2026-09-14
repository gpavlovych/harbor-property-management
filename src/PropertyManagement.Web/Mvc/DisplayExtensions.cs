using PropertyManagement.Application.Applications.Models;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Web.Mvc;

public static class DisplayExtensions
{
    public static string BadgeClass(this ApplicationStatus status) => status switch
    {
        ApplicationStatus.Draft => "text-bg-secondary",
        ApplicationStatus.Submitted => "text-bg-primary",
        ApplicationStatus.Returned => "text-bg-warning",
        ApplicationStatus.Approved => "text-bg-success",
        ApplicationStatus.Denied => "text-bg-danger",
        ApplicationStatus.Withdrawn => "text-bg-dark",
        _ => "text-bg-light"
    };

    public static string Title(this ApplicationSection section) => section switch
    {
        ApplicationSection.ApplicantInformation => "Applicant Information",
        ApplicationSection.ResidenceHistory => "Residence History",
        ApplicationSection.Summary => "Summary",
        _ => section.ToString()
    };

    public static string Local(this DateTime utc) => utc.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
    public static string Short(this DateOnly date) => date.ToString("MMM d, yyyy");
    public static string Money(this decimal amount) => amount.ToString("C0");
}
