using PropertyManagement.Application.Applications.Models;

namespace PropertyManagement.Application.Applications;

/// <summary>Use cases that move a rental application through its lifecycle.</summary>
public interface IRentalApplicationService
{
    /// <summary>Creates a draft for the unit or returns the id of the applicant's existing open application for it.</summary>
    Task<int> StartAsync(int unitId, string applicantId, CancellationToken ct = default);

    Task SaveApplicantInformationAsync(int applicationId, string applicantId, ApplicantInformationInput input, CancellationToken ct = default);
    Task CompleteResidenceHistoryAsync(int applicationId, string applicantId, CancellationToken ct = default);

    Task<int> AddResidenceAsync(string applicantId, ResidenceInput input, CancellationToken ct = default);
    Task UpdateResidenceAsync(string applicantId, ResidenceInput input, CancellationToken ct = default);
    Task RemoveResidenceAsync(int applicationId, int residenceId, string applicantId, CancellationToken ct = default);

    Task SubmitAsync(int applicationId, string applicantId, CancellationToken ct = default);
    Task WithdrawAsync(int applicationId, string applicantId, string? comment, CancellationToken ct = default);

    Task ReviewAsync(ReviewInput input, string managerId, CancellationToken ct = default);
    Task SaveManagerNotesAsync(int applicationId, string? notes, CancellationToken ct = default);
}
