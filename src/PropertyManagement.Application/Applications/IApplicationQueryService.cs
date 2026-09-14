using PropertyManagement.Application.Applications.Models;

namespace PropertyManagement.Application.Applications;

/// <summary>Read side: list, single-page details, residences, history and leases.</summary>
public interface IApplicationQueryService
{
    Task<ApplicationListResult> ListAsync(ApplicationListFilter filter, string? restrictToApplicantId, CancellationToken ct = default);
    Task<ApplicationDetails?> GetDetailsAsync(int applicationId, string userId, bool isManager, ApplicationSection? requestedSection, CancellationToken ct = default);
    Task<ResidenceList?> GetResidenceListAsync(int applicationId, string userId, bool isManager, CancellationToken ct = default);
    Task<ResidenceItem?> GetResidenceAsync(int applicationId, int residenceId, string applicantId, CancellationToken ct = default);
    Task<List<HistoryEntry>> GetHistoryAsync(int applicationId, CancellationToken ct = default);
    Task<int> CountPendingReviewAsync(CancellationToken ct = default);
    Task<List<LeaseItem>> ListLeasesAsync(string? restrictToTenantId, CancellationToken ct = default);
}
