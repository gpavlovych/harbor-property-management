using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Applications.Models;
using PropertyManagement.Application.Common.Interfaces;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Rules;

namespace PropertyManagement.Application.Applications;

public class ApplicationQueryService(IApplicationDbContext db, TimeProvider time) : IApplicationQueryService
{
    public const int PageSize = 10;

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    public async Task<ApplicationListResult> ListAsync(ApplicationListFilter filter, string? restrictToApplicantId, CancellationToken ct = default)
    {
        var query = db.RentalApplications.AsNoTracking().AsQueryable();

        // Filtering happens in SQL, never in memory.
        if (restrictToApplicantId is not null) query = query.Where(a => a.ApplicantId == restrictToApplicantId);
        if (filter.Status.HasValue) query = query.Where(a => a.Status == filter.Status);
        if (filter.PropertyId.HasValue) query = query.Where(a => a.Unit.PropertyId == filter.PropertyId);

        var total = await query.CountAsync(ct);

        var joined = from a in query
                     join u in db.Users on a.ApplicantId equals u.Id
                     select new { Application = a, ApplicantName = u.FullName };

        joined = (filter.Sort, filter.Desc) switch
        {
            (ApplicationSort.Applicant, false) => joined.OrderBy(x => x.ApplicantName),
            (ApplicationSort.Applicant, true) => joined.OrderByDescending(x => x.ApplicantName),
            (ApplicationSort.Property, false) => joined.OrderBy(x => x.Application.Unit.Property.Name).ThenBy(x => x.Application.Unit.UnitNumber),
            (ApplicationSort.Property, true) => joined.OrderByDescending(x => x.Application.Unit.Property.Name).ThenByDescending(x => x.Application.Unit.UnitNumber),
            (ApplicationSort.Status, false) => joined.OrderBy(x => x.Application.Status),
            (ApplicationSort.Status, true) => joined.OrderByDescending(x => x.Application.Status),
            (ApplicationSort.Updated, false) => joined.OrderBy(x => x.Application.UpdatedAt),
            _ => joined.OrderByDescending(x => x.Application.UpdatedAt)
        };

        var page = Math.Max(1, filter.Page);
        var items = await joined
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new ApplicationListItem
            {
                Id = x.Application.Id,
                ApplicantName = x.ApplicantName,
                PropertyName = x.Application.Unit.Property.Name,
                UnitNumber = x.Application.Unit.UnitNumber,
                Status = x.Application.Status,
                CreatedAt = x.Application.CreatedAt,
                UpdatedAt = x.Application.UpdatedAt,
                SubmittedAt = x.Application.SubmittedAt
            })
            .ToListAsync(ct);

        return new ApplicationListResult
        {
            Filter = filter with { Page = page },
            Results = new PagedResult<ApplicationListItem> { Items = items, TotalCount = total, Page = page, PageSize = PageSize },
            Properties = await db.Properties.OrderBy(p => p.Name).Select(p => new LookupItem { Id = p.Id, Name = p.Name }).ToListAsync(ct)
        };
    }

    public async Task<ApplicationDetails?> GetDetailsAsync(int applicationId, string userId, bool isManager, ApplicationSection? requestedSection, CancellationToken ct = default)
    {
        var a = await db.RentalApplications.AsNoTracking()
            .Include(x => x.Unit).ThenInclude(u => u.Property)
            .Include(x => x.Unit).ThenInclude(u => u.UnitType)
            .Include(x => x.Unit).ThenInclude(u => u.Leases)
            .Include(x => x.Residences)
            .Include(x => x.Lease)
            .FirstOrDefaultAsync(x => x.Id == applicationId, ct);

        if (a is null) return null;
        var isOwner = a.ApplicantId == userId;
        if (!isOwner && !isManager) return null;

        var applicant = await db.Users.FirstOrDefaultAsync(u => u.Id == a.ApplicantId, ct);

        // Editing is decided here, on the server: only the owner while Draft or Returned.
        var isEditable = isOwner && a.IsEditable;
        var unitAvailable = a.Unit.IsAvailableOn(Today);

        var blocking = new List<string>();
        if (!a.ApplicantInformationCompletedAt.HasValue) blocking.Add("Applicant Information has not been completed.");
        if (!a.ResidenceHistoryCompletedAt.HasValue) blocking.Add("Residence History has not been completed.");
        if (!unitAvailable && !a.IsTerminal) blocking.Add("The unit has an active lease and is no longer available.");

        return new ApplicationDetails
        {
            Id = a.Id,
            Status = a.Status,
            PropertyName = a.Unit.Property.Name,
            PropertyAddress = a.Unit.Property.FullAddress,
            UnitNumber = a.Unit.UnitNumber,
            UnitType = a.Unit.UnitType.Name,
            Bedrooms = a.Unit.Bedrooms,
            MonthlyRent = a.Unit.MonthlyRent,
            UnitIsAvailable = unitAvailable,
            ApplicantName = applicant?.FullName ?? string.Empty,
            ApplicantEmail = applicant?.Email ?? string.Empty,
            CurrentSection = requestedSection ?? DefaultSection(a, isEditable),
            IsEditable = isEditable,
            IsOwner = isOwner,
            IsManager = isManager,
            CanSubmit = isOwner && ApplicationWorkflow.CanSubmit(a.Status) && a.AllSectionsCompleted && unitAvailable,
            CanWithdraw = isOwner && ApplicationWorkflow.CanWithdraw(a.Status),
            CanReview = isManager && ApplicationWorkflow.CanReview(a.Status),
            ApplicantInformationCompleted = a.ApplicantInformationCompletedAt.HasValue,
            ResidenceHistoryCompleted = a.ResidenceHistoryCompletedAt.HasValue,
            BlockingIssues = blocking,
            ApplicantInformation = new ApplicantInformationInput(a.FullName, a.Phone, a.Email, a.CurrentAddress),
            Residences = ToResidenceList(a, isEditable),
            ReviewComment = a.ReviewComment,
            ManagerNotes = isManager ? a.ManagerNotes : null,
            CreatedAt = a.CreatedAt,
            SubmittedAt = a.SubmittedAt,
            ReviewedAt = a.ReviewedAt,
            Lease = a.Lease is null ? null : new LeaseSummary { StartDate = a.Lease.StartDate, EndDate = a.Lease.EndDate, MonthlyRent = a.Lease.MonthlyRent }
        };
    }

    private static ApplicationSection DefaultSection(RentalApplication a, bool isEditable)
    {
        if (!isEditable) return ApplicationSection.Summary;
        if (!a.ApplicantInformationCompletedAt.HasValue) return ApplicationSection.ApplicantInformation;
        if (!a.ResidenceHistoryCompletedAt.HasValue) return ApplicationSection.ResidenceHistory;
        return ApplicationSection.Summary;
    }

    public async Task<ResidenceList?> GetResidenceListAsync(int applicationId, string userId, bool isManager, CancellationToken ct = default)
    {
        var a = await db.RentalApplications.AsNoTracking()
            .Include(x => x.Residences)
            .FirstOrDefaultAsync(x => x.Id == applicationId, ct);
        if (a is null) return null;
        var isOwner = a.ApplicantId == userId;
        if (!isOwner && !isManager) return null;

        return ToResidenceList(a, isOwner && a.IsEditable);
    }

    public Task<ResidenceItem?> GetResidenceAsync(int applicationId, int residenceId, string applicantId, CancellationToken ct = default) =>
        db.Residences.AsNoTracking()
            .Where(r => r.Id == residenceId && r.RentalApplicationId == applicationId && r.RentalApplication.ApplicantId == applicantId)
            .Select(r => new ResidenceItem
            {
                Id = r.Id, ApplicationId = r.RentalApplicationId, Address = r.Address, LandlordName = r.LandlordName,
                LandlordPhone = r.LandlordPhone, MoveInDate = r.MoveInDate, MoveOutDate = r.MoveOutDate
            })
            .FirstOrDefaultAsync(ct);

    public async Task<List<HistoryEntry>> GetHistoryAsync(int applicationId, CancellationToken ct = default) =>
        await (from h in db.ApplicationStatusHistory.AsNoTracking()
               join u in db.Users on h.ChangedByUserId equals u.Id
               where h.RentalApplicationId == applicationId
               orderby h.ChangedAt descending, h.Id descending
               select new HistoryEntry
               {
                   FromStatus = h.FromStatus,
                   ToStatus = h.ToStatus,
                   ChangedBy = u.FullName,
                   ChangedAt = h.ChangedAt,
                   Comment = h.Comment
               }).ToListAsync(ct);

    public Task<int> CountPendingReviewAsync(CancellationToken ct = default) =>
        db.RentalApplications.CountAsync(a => a.Status == ApplicationStatus.Submitted, ct);

    public async Task<List<LeaseItem>> ListLeasesAsync(string? restrictToTenantId, CancellationToken ct = default)
    {
        var today = Today;
        var query = db.Leases.AsNoTracking().AsQueryable();
        if (restrictToTenantId is not null) query = query.Where(l => l.TenantId == restrictToTenantId);

        return await (from l in query
                      join u in db.Users on l.TenantId equals u.Id
                      orderby l.StartDate descending
                      select new LeaseItem
                      {
                          Id = l.Id,
                          ApplicationId = l.RentalApplicationId,
                          TenantName = u.FullName,
                          PropertyName = l.Unit.Property.Name,
                          UnitNumber = l.Unit.UnitNumber,
                          StartDate = l.StartDate,
                          EndDate = l.EndDate,
                          MonthlyRent = l.MonthlyRent,
                          IsActive = l.StartDate <= today && l.EndDate >= today
                      }).ToListAsync(ct);
    }

    private static ResidenceList ToResidenceList(RentalApplication a, bool isEditable) => new()
    {
        ApplicationId = a.Id,
        IsEditable = isEditable,
        Residences = a.Residences.OrderByDescending(r => r.MoveOutDate).Select(ToItem).ToList()
    };

    private static ResidenceItem ToItem(Residence r) => new()
    {
        Id = r.Id, ApplicationId = r.RentalApplicationId, Address = r.Address, LandlordName = r.LandlordName,
        LandlordPhone = r.LandlordPhone, MoveInDate = r.MoveInDate, MoveOutDate = r.MoveOutDate
    };
}
