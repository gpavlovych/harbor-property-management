using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Domain.Tests;

public class ApplicationWorkflowTests
{
    [Theory]
    [InlineData(ApplicationStatus.Draft, ApplicationStatus.Submitted, true)]
    [InlineData(ApplicationStatus.Draft, ApplicationStatus.Withdrawn, true)]
    [InlineData(ApplicationStatus.Draft, ApplicationStatus.Approved, false)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Approved, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Returned, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Denied, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Withdrawn, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Draft, false)]
    [InlineData(ApplicationStatus.Returned, ApplicationStatus.Submitted, true)]
    [InlineData(ApplicationStatus.Returned, ApplicationStatus.Withdrawn, true)]
    [InlineData(ApplicationStatus.Returned, ApplicationStatus.Approved, false)]
    [InlineData(ApplicationStatus.Approved, ApplicationStatus.Withdrawn, false)]
    [InlineData(ApplicationStatus.Denied, ApplicationStatus.Submitted, false)]
    [InlineData(ApplicationStatus.Withdrawn, ApplicationStatus.Submitted, false)]
    public void CanTransition_follows_the_state_machine(ApplicationStatus from, ApplicationStatus to, bool expected) =>
        Assert.Equal(expected, ApplicationWorkflow.CanTransition(from, to));

    [Theory]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Denied)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void Approved_Denied_and_Withdrawn_are_terminal(ApplicationStatus status)
    {
        Assert.True(ApplicationWorkflow.IsTerminal(status));
        Assert.Contains(status, ApplicationWorkflow.TerminalStatuses);
        Assert.DoesNotContain(status, ApplicationWorkflow.OpenStatuses);
        foreach (var target in Enum.GetValues<ApplicationStatus>())
            Assert.False(ApplicationWorkflow.CanTransition(status, target));
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft, true)]
    [InlineData(ApplicationStatus.Returned, true)]
    [InlineData(ApplicationStatus.Submitted, false)]
    [InlineData(ApplicationStatus.Approved, false)]
    [InlineData(ApplicationStatus.Denied, false)]
    [InlineData(ApplicationStatus.Withdrawn, false)]
    public void Only_Draft_and_Returned_are_editable(ApplicationStatus status, bool expected) =>
        Assert.Equal(expected, ApplicationWorkflow.IsEditable(status));

    [Fact]
    public void Only_Submitted_can_be_reviewed()
    {
        foreach (var status in Enum.GetValues<ApplicationStatus>())
            Assert.Equal(status == ApplicationStatus.Submitted, ApplicationWorkflow.CanReview(status));
    }

    [Theory]
    [InlineData(ReviewOutcome.Approve, ApplicationStatus.Approved, false)]
    [InlineData(ReviewOutcome.Return, ApplicationStatus.Returned, true)]
    [InlineData(ReviewOutcome.Deny, ApplicationStatus.Denied, true)]
    public void Review_outcomes_map_to_statuses_and_comment_rules(ReviewOutcome outcome, ApplicationStatus status, bool commentRequired)
    {
        Assert.Equal(status, outcome.ToStatus());
        Assert.Equal(commentRequired, ApplicationWorkflow.RequiresComment(outcome));
    }
}
