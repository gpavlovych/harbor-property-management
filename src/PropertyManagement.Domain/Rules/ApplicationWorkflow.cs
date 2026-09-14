using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Rules;

/// <summary>Pure rules for the application state machine. No I/O, fully unit-testable.</summary>
public static class ApplicationWorkflow
{
    private static readonly IReadOnlyDictionary<ApplicationStatus, ApplicationStatus[]> Transitions =
        new Dictionary<ApplicationStatus, ApplicationStatus[]>
        {
            [ApplicationStatus.Draft] = [ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Submitted] = [ApplicationStatus.Approved, ApplicationStatus.Returned, ApplicationStatus.Denied, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Returned] = [ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
            [ApplicationStatus.Approved] = [],
            [ApplicationStatus.Denied] = [],
            [ApplicationStatus.Withdrawn] = []
        };

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static bool IsTerminal(ApplicationStatus status) => Transitions[status].Length == 0;

    public static readonly ApplicationStatus[] TerminalStatuses =
        Enum.GetValues<ApplicationStatus>().Where(IsTerminal).ToArray();

    public static readonly ApplicationStatus[] OpenStatuses =
        Enum.GetValues<ApplicationStatus>().Where(s => !IsTerminal(s)).ToArray();

    /// <summary>An applicant may edit the application only while it is a Draft or has been Returned.</summary>
    public static bool IsEditable(ApplicationStatus status) =>
        status is ApplicationStatus.Draft or ApplicationStatus.Returned;

    public static bool CanWithdraw(ApplicationStatus status) => CanTransition(status, ApplicationStatus.Withdrawn);

    public static bool CanSubmit(ApplicationStatus status) => CanTransition(status, ApplicationStatus.Submitted);

    public static bool CanReview(ApplicationStatus status) => status == ApplicationStatus.Submitted;

    public static ApplicationStatus ToStatus(this ReviewOutcome outcome) => outcome switch
    {
        ReviewOutcome.Approve => ApplicationStatus.Approved,
        ReviewOutcome.Return => ApplicationStatus.Returned,
        ReviewOutcome.Deny => ApplicationStatus.Denied,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    public static bool RequiresComment(ReviewOutcome outcome) => outcome is ReviewOutcome.Return or ReviewOutcome.Deny;
}
