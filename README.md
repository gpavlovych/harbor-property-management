# Harbor Property Management

Technical assessment for the .NET Developer role: a full-stack ASP.NET Core MVC application where a
property management company accepts rental applications for its apartments. Property managers maintain
properties and units and review applications; applicants browse available units, fill out and submit an
application, and receive a twelve-month lease when approved.

## Stack

| Concern | Choice |
| --- | --- |
| Runtime | .NET 10, ASP.NET Core MVC with Razor views, partial views and view components |
| Data | SQL Server (LocalDB by default) via Entity Framework Core 10, code-first migrations |
| Identity | ASP.NET Core Identity with two roles: `Applicant` and `PropertyManager` |
| Seeding | Bogus with a fixed seed; runs idempotently on every start |
| Front end | Bootstrap 5, jQuery unobtrusive validation, a small vanilla-JS modal helper (`wwwroot/js/site.js`) |
| Tests | xUnit, EF Core in-memory provider for service tests |

## Screencast

[docs/screencast.mp4](docs/screencast.mp4) is a two-and-a-half-minute walkthrough of the running app: browsing, the
property and unit modals with validation, the review modal rejecting an approval on a leased unit, an applicant filling
out and submitting the sectioned application, and an approval issuing a lease.

## Running it

Prerequisites: the .NET 10 SDK and a SQL Server instance. The default connection string targets
SQL Server LocalDB, which ships with Visual Studio and the SQL Server Express installer.

```bash
dotnet run --project src/PropertyManagement.Web
```

Then open <http://localhost:5139>. On start the app creates the database, applies migrations and seeds it.
Nothing else needs to be installed or run by hand.

To use SQL Server Express (or any other instance) instead of LocalDB, change `ConnectionStrings:DefaultConnection`
in `src/PropertyManagement.Web/appsettings.json`, for example:

```text
Server=.\SQLEXPRESS;Database=PropertyManagement;Trusted_Connection=True;TrustServerCertificate=True
```

Run the tests with:

```bash
dotnet test
```

### Running with Docker

No SDK or SQL Server needed, only Docker. From the repository root:

```bash
docker compose up --build
```

Then open <http://localhost:8080>. Compose starts a SQL Server 2022 Express container, waits for its health check,
builds the web app with a multi-stage `Dockerfile`, and starts it. The app applies migrations and seeds on start
exactly as it does locally; the database persists in the `mssql-data` volume between runs.

The `sa` password defaults to `Harbor_Pm_2026!` and can be overridden with the `MSSQL_SA_PASSWORD` environment variable
(SQL Server rejects weak passwords, so keep it long with mixed character classes). The database is also reachable from
the host at `localhost,1433` with that login.

To start over with an empty database:

```bash
docker compose down -v
```

### Demo accounts

All seeded accounts use the password `Password1!`.

| Role | Email |
| --- | --- |
| Property manager | `manager1@example.com`, `manager2@example.com` |
| Applicant | `applicant1@example.com` … `applicant6@example.com` |

The seed also creates five properties with units, the unit-type lookup (two values are inactive), and
applications in every status: Draft, Submitted, Returned, Approved (with leases), Denied and Withdrawn.
One submitted application targets a unit that already has an active lease so the approval guard can be
demonstrated.

## How it is put together

The solution follows Clean Architecture. Dependencies point inwards and are enforced by project references:
Web depends on Application and Infrastructure, Infrastructure depends on Application, Application depends on
Domain, and Domain depends on nothing.

```
src/PropertyManagement.Domain          no package references
  Entities/        Property, Unit, UnitType, RentalApplication (aggregate root), Residence, Lease,
                   ApplicationStatusHistory. Users are referenced by id only; the domain has no user entity.
  Rules/           ApplicationWorkflow (state machine) and LeaseRules (term, availability, overlap): pure functions
  Enums/           ApplicationStatus, ReviewOutcome
  Exceptions/      DomainException (rule violated, safe to show), EntityNotFoundException

src/PropertyManagement.Application     references Domain and the EF Core abstractions
  Common/          IApplicationDbContext (the persistence port), UserSummary (the only view of an identity user
                   the use cases need), PagedResult, LookupItem, Roles
  Catalog/         ICatalogService + CatalogService: properties, units, unit-type lookup, unit browsing
  Applications/    IRentalApplicationService + RentalApplicationService (commands: start, save section, residences,
                   submit, withdraw, review, notes) and IApplicationQueryService + ApplicationQueryService (read models:
                   list, single-page details, history, leases). Inputs are records; results are plain read models.

src/PropertyManagement.Infrastructure  references Application; EF Core SQL Server, ASP.NET Identity, Bogus
  Persistence/     ApplicationDbContext implementing IApplicationDbContext, one IEntityTypeConfiguration per entity
                   (lengths, precision, indexes, delete behaviour, the foreign keys to Identity users), migrations
  Identity/        ApplicationUser : IdentityUser
  Seeding/         DbSeeder (migrate + idempotent Bogus seed)
  DependencyInjection.cs   AddInfrastructure(configuration), InitialiseDatabaseAsync()

src/PropertyManagement.Web             references Application and Infrastructure (the latter only for DI and Identity)
  Controllers/     thin: bind a view model, call one use case, translate DomainException into model state
  ViewModels/      forms with validation attributes; each maps to an Application input via ToInput()/From()
  ViewComponents/  UnitsTable, ResidenceHistorySection, ApplicationHistory, PendingReviewCount
  Views/           Razor views and partials; Views/Applications holds the single-page application
  Mvc/             modal response conventions, model-state helpers, display helpers

tests/PropertyManagement.Domain.Tests        41 tests: state machine, lease rules, aggregate behaviour
tests/PropertyManagement.Application.Tests   33 tests: use cases and queries against the EF in-memory provider
tests/PropertyManagement.Web.Tests            7 tests: view model validation and mapping
```

Design notes:

- The persistence port is `IApplicationDbContext` rather than a repository per aggregate. EF Core's `DbSet` is already
  a unit-of-work and specification abstraction, so wrapping it would only hide the query capabilities the use cases
  need for SQL-side filtering and paging.
- Identity stays in Infrastructure. Domain entities hold user ids, the port exposes users as an `IQueryable<UserSummary>`
  projection, and queries join to it for display names. Foreign keys to the users table are configured in
  Infrastructure without navigation properties on the domain side.
- State changes go through `RentalApplication.TransitionTo`, which validates the transition and appends history, so a
  status can never change without an audit entry. Lease issuance is `RentalApplication.IssueLease`.
- Presentation concerns (data annotations, section wizard, modal conventions) never reach the Application layer, and
  the Application layer never references ASP.NET Core.

### Modals

Every create, edit, remove, review, withdraw and notes action is a modal populated from a partial view
returned by a controller action. The form inside the modal posts with `fetch`; the response headers tell the
page what to do (`Infrastructure/ModalControllerExtensions.cs` and `wwwroot/js/site.js` share this contract):

- no header: the partial is re-rendered inside the modal with its validation messages;
- `X-Modal-Result: refresh` plus `X-Modal-Target`: the modal closes and the element matching the target
  selector is replaced by the response HTML (a partial view or a view component);
- `X-Modal-Result: redirect`: the modal closes and the browser navigates.

Anti-forgery tokens are rendered by the form tag helper and travel with the `FormData`, so every POST is
validated.

### The rental application page

One page, one view model (`ApplicationPageViewModel`), one form, one action (`Applications/Section`).
Each section renders through its own partial or view component:

- Applicant Information: partial view `_ApplicantInformationSection`;
- Residence History: view component `ResidenceHistorySection`, whose residences are added, edited and removed
  through a modal that refreshes only the residence list;
- Summary: partial view `_SummarySection`, which reuses the read-only partials of both sections.

The button clicked sets the `Command` field. **Continue** validates the current section, persists it only when
valid (and stamps the section as completed), then shows the next section. When the section is invalid the same
section re-renders with its errors. **Back** never saves. **Submit** is only enabled on the Summary once both
sections have been completed and the unit is still available; the reasons blocking submission are listed.

Whether a section renders editable or read-only is decided on the server: only the owner may edit, and only
while the application is Draft or Returned. Section posts for any other state, or by any other user, are
rejected (403).

### Business rules

- **Statuses** are Draft, Submitted, Returned, Approved, Denied and Withdrawn. Approved, Denied and Withdrawn
  are terminal. Allowed transitions live in `ApplicationWorkflow` and every transition is written to
  `ApplicationStatusHistory` (who, when, from, to, comment). Property managers see this history on the
  application page.
- **Review** happens in a modal with an outcome of Approve, Return or Deny and a comment. The comment is required
  for Return and Deny, and a lease start date is required for Approve. Validation is defined once on
  `ReviewViewModel` and enforced again in the service.
- **Leases** last twelve months (`LeaseRules.EndDateFor`). A unit whose lease term covers today is not available:
  it is excluded from browsing and from starting applications. Submitting an application and approving it both
  re-check the unit and reject the action with an error when an active lease exists. Approval additionally
  rejects any new term that would overlap an existing lease, so a second lease can never be issued. Other open
  applications for the unit are left as they are.
- **Unit types** are a lookup with active and inactive values. Inactive values still display on units that use
  them and remain selectable for that unit only. The rule is enforced in `CatalogService`, not just in the UI.
- **Application list** filtering by status and property, sorting and paging all run in SQL. Applicants see only
  their own applications; property managers see all of them.
- **Removing** a property or unit is blocked while it has leases or applications.

### Bonus items included

- Sorting and paging of the application list, performed in the database.
- Property manager notes on an application, editable through a modal, stored separately from review comments
  and never rendered to applicants.

Not implemented: the JSON grid endpoint with OpenAPI, the review queue with claim/release, saving invalid
sections, and multi-applicant applications with concurrency handling.

### Assumptions

- At least one prior residence is required to complete the Residence History section. Each residence needs a
  move-in date, a move-out date after it, and a move-out date that is not in the future.
- Sign-up lets the user pick Applicant or Property Manager, as the assessment asks. In production the manager
  role would be granted by an administrator.
- Money and dates render in `en-US` regardless of the host machine's locale.
