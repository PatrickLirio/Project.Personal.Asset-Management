# IT Asset Management Service
## Step-by-Step Refactor and Build Guide

### .NET 10 • SQL Server • SSMS • Admin and User roles

**Prepared 16 November 2026**  
This tutorial uses the existing Asset Management process and the Base/Preschool projects as architecture references. It is written for a beginner learning to implement a production-minded .NET service one layer at a time.

## How to use this guide

Work through the numbered steps in order. Run commands from the repository root unless the step says otherwise. Read the short explanation before copying a code sample: each sample shows which project should own that responsibility and why.

The snippets are focused examples, not a complete replacement repository. Keep the existing project namespaces and signatures consistent as you move code. After each refactoring step, build the solution and fix the first error before moving on. Use a development database while learning; review generated SQL in SSMS before applying it to any database with important data.

---

## Start here: yes, this is the target structure

The service structure you highlighted is the intended destination:

```text
src/Services/AssetService/
  Itam.AssetService.Shell/          # Runnable web host and Program.cs
  Itam.AssetService.Api/            # HTTP controllers
  Itam.AssetService.Application/    # Use cases and repository interfaces
  Itam.AssetService.Core/           # Business entities and rules
  Itam.AssetService.Infrastructure/ # EF Core, SQL Server, Identity, RabbitMQ adapters
```

The solution also keeps the existing shared projects:

```text
src/Common/
  Itam.Common.Base/
  Itam.Common.Configuration/
  Itam.Common.Messaging/
```

So the **Asset Service has five projects**. `Core.Contracts` and `Infrastructure.Contracts` are not part of the target. Move repository interfaces into Application, move business entities and enums into Core, then remove those two old projects after nothing references them. Keep Shell and API separate because that is the hosting pattern in your reference guide.

```text
                    ┌─────────────────────────┐
                    │ Shell (runnable host)   │
                    └──────┬──────────┬───────┘
                           │          │
                 ┌─────────▼──┐   ┌───▼────────────────┐
                 │ API        │   │ Infrastructure     │
                 │ controllers│   │ EF, SQL, Identity  │
                 └──────┬─────┘   └────┬───────────┬────┘
                        │              │           │
                 ┌──────▼──────────────▼──┐        │
                 │ Application (use cases)│        │
                 └────────────┬───────────┘        │
                              │                    │
                       ┌──────▼────────────────────▼┐
                       │ Core (asset business rules)│
                       └────────────────────────────┘
```

**Arrows mean “references.”** Core has no reference to EF Core, ASP.NET, or RabbitMQ. Application refers to Core. Infrastructure implements the interfaces declared in Application. Shell assembles the pieces when the app starts.

### What comes from Base and Preschool?

- Follow Base’s separation of Core, Application, and Infrastructure and its per-layer dependency registration.
- Follow Preschool’s API/Application/Core/Infrastructure arrangement and domain-oriented folders.
- Keep the Asset Management business process from this repository. These are architecture references; do not add references to the Base or Preschool repositories.
- Do not copy every abstraction from those projects. Use specific asset use cases instead of a generic service for every table.

---

## 1. Features and permissions this guide implements

The existing flow remains: register asset, search/list assets, assign, return, and publish existing integration events. The selected additions are asset requests, issue reports, transfers, repairs, retirement, and Admin/User authentication. Tester is deferred.

| Capability | Admin | User |
|---|---|---|
| Account | Create/edit/deactivate accounts and assign Admin/User roles | Self-register; server always assigns User role |
| Asset catalogue | Create, view all, edit, assign, return, transfer, repair, retire | View only assets assigned to their linked Custodian profile |
| Asset requests | Review, approve, reject, and fulfill requests | Submit and see own requests |
| Issue reports | See all reports and manage repair/resolution | Report and see issues for own assigned assets |
| Other users' assets | May see all | Cannot view or change them |

Treat Admin “delete” of users or assets as **deactivation/retirement**, not a physical database delete. Historical assignment and audit records must remain.

### Assumptions for this implementation

1. Self-registration creates an Identity account with the `User` role. A client cannot send a role to gain Admin.
2. User report of an asset’s “status” means a condition/issue report for an asset currently assigned to that User.
3. Admin links an Identity login to the existing employee/Custodian row after checking the employee number or email.
4. User may submit a new asset request before the Custodian profile is linked, but cannot see legacy assignments until an Admin links the account.
5. Admin controls actual assignments, transfers, repairs, and retirement. A User requests assets; a User does not directly assign one to themselves.

---

## 2. Prepare the repository before moving code

### Step 2.1 — Create a branch and capture the current behavior

Run from the repository root in PowerShell:

```powershell
git switch -c refactor/asset-service-layers
dotnet build .\Itam.AssetManagement.slnx
```

Write down current routes, request fields, response fields, and RabbitMQ events before changing files. The existing solution currently lists the contract/Core/Infrastructure projects, but it omits the API and Shell projects. Add those projects as shown below so a solution build includes the runnable app.

Back up development data before schema work. A refactor should preserve existing assets and assignments.

### Step 2.2 — Fix the database-options mismatch

The current `DatabaseOptions` class has `ConnectionString`, while `appsettings.json` and `Program.cs` use `Database:BizDb`. Make those names agree before wiring the new layers. This guide keeps the existing appsettings key and changes the options class to match it:

```csharp
// src/Common/Itam.Common.Configuration/Options/DatabaseOptions.cs
using System.ComponentModel.DataAnnotations;

namespace Itam.Common.Configuration.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string BizDb { get; set; } = string.Empty;

    public string? AuditDb { get; set; }
    public string? TranDb { get; set; }

    [Range(5, 120)]
    public int CommandTimeoutSeconds { get; set; } = 30;
}
```

The configuration binder maps JSON property names to C# properties. If the names differ, the value is empty and the database connection fails at startup.

Do not commit SQL passwords, the JWT signing key, or RabbitMQ passwords. Put local secrets in User Secrets and deployment secrets in the deployment secret store.

The current Shell `appsettings.json` contains local connection and broker credentials plus a JWT key. Move those values to User Secrets or environment-specific secret storage before committing or deploying. Keep only non-sensitive defaults in `appsettings.json`.

### Step 2.3 — Check the shared paging helper

The current `PagedResult<T>.HasNextPage` compares the page number to the total row count. It should compare to the total number of pages; otherwise paging metadata is wrong when a result has more than one row per page:

```csharp
public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
public bool HasNextPage => PageNumber < TotalPages;
```

Make this small correction in `Itam.Common.Base` before relying on the paging response. A shared helper is used by multiple services, so fix it once in the shared project.

---

## 3. Create and wire the five-project service

### Step 3.1 — Add existing API and Shell projects to the solution

```powershell
dotnet sln .\Itam.AssetManagement.slnx add `
  .\src\Services\AssetService\Itam.AssetService.Api\Itam.AssetService.Api.csproj

dotnet sln .\Itam.AssetManagement.slnx add `
  .\src\Services\AssetService\Itam.AssetService.Shell\Itam.AssetService.Shell.csproj
```

If either command reports that the project is already present, inspect the `.slnx` and continue. The API is a class library. The Shell is the executable `Microsoft.NET.Sdk.Web` project.

### Step 3.2 — Create Application

```powershell
dotnet new classlib `
  --name Itam.AssetService.Application `
  --framework net10.0 `
  --output .\src\Services\AssetService\Itam.AssetService.Application

dotnet sln .\Itam.AssetManagement.slnx add `
  .\src\Services\AssetService\Itam.AssetService.Application\Itam.AssetService.Application.csproj
```

Delete the generated `Class1.cs` after the project is added. Application holds use cases such as `AssignAsset` and interfaces such as `IAssetRepository`.

### Step 3.3 — Add project references

```powershell
dotnet add .\src\Services\AssetService\Itam.AssetService.Api\Itam.AssetService.Api.csproj `
  reference .\src\Services\AssetService\Itam.AssetService.Application\Itam.AssetService.Application.csproj

dotnet add .\src\Services\AssetService\Itam.AssetService.Application\Itam.AssetService.Application.csproj `
  reference .\src\Services\AssetService\Itam.AssetService.Core\Itam.AssetService.Core.csproj

dotnet add .\src\Services\AssetService\Itam.AssetService.Application\Itam.AssetService.Application.csproj `
  reference .\src\Common\Itam.Common.Base\Itam.Common.Base.csproj

dotnet add .\src\Services\AssetService\Itam.AssetService.Infrastructure\Itam.AssetService.Infrastructure.csproj `
  reference .\src\Services\AssetService\Itam.AssetService.Application\Itam.AssetService.Application.csproj

dotnet add .\src\Services\AssetService\Itam.AssetService.Infrastructure\Itam.AssetService.Infrastructure.csproj `
  reference .\src\Services\AssetService\Itam.AssetService.Core\Itam.AssetService.Core.csproj

dotnet add .\src\Services\AssetService\Itam.AssetService.Infrastructure\Itam.AssetService.Infrastructure.csproj `
  reference .\src\Common\Itam.Common.Configuration\Itam.Common.Configuration.csproj

dotnet add .\src\Services\AssetService\Itam.AssetService.Shell\Itam.AssetService.Shell.csproj `
  reference .\src\Services\AssetService\Itam.AssetService.Application\Itam.AssetService.Application.csproj
```

Shell already references API and Infrastructure. Core can reference Common.Base because its audit base is framework-independent. Infrastructure uses Common.Configuration for `DatabaseOptions`. If RabbitMQ events remain, add a reference from Infrastructure to Common.Messaging. Keep SQL Server, Identity, EF Core, ASP.NET, and MassTransit packages out of Core.

Use this as the final project-reference map. Remove redundant direct references left over from the current structure so the project files teach the same boundaries as the source folders:

| Project | Project references it should keep |
|---|---|
| Core | Common.Base |
| Application | Core, Common.Base |
| Infrastructure | Application, Core, Common.Base, Common.Configuration; Common.Messaging if it publishes the existing events |
| API | Application, plus its existing `Microsoft.AspNetCore.App` framework reference |
| Shell | API, Application, Infrastructure, Common.Configuration |

The API should not reference Infrastructure. Core should not reference Infrastructure, API, Shell, or messaging. Shell is the composition root: it references the outer projects so it can register them, but it contains no asset rules or database queries.

For example, remove references that only exist because of the old Contracts projects after you move their code:

```powershell
dotnet remove .\src\Services\AssetService\Itam.AssetService.Api\Itam.AssetService.Api.csproj `
  reference .\src\Services\AssetService\Itam.AssetService.Core.Contracts\Itam.AssetService.Core.Contracts.csproj

dotnet remove .\src\Services\AssetService\Itam.AssetService.Infrastructure\Itam.AssetService.Infrastructure.csproj `
  reference .\src\Services\AssetService\Itam.AssetService.Infrastructure.Contracts\Itam.AssetService.Infrastructure.Contracts.csproj

dotnet remove .\src\Services\AssetService\Itam.AssetService.Shell\Itam.AssetService.Shell.csproj `
  reference .\src\Services\AssetService\Itam.AssetService.Core\Itam.AssetService.Core.csproj
```

Inspect every `.csproj` before removing a shared reference. Keep `Common.Configuration` in Shell if `Program.cs` binds its option types there; keep `Common.Messaging` in Infrastructure only when the RabbitMQ adapter uses its message contracts.

### Step 3.4 — Move the current files

Use Visual Studio’s Move/Refactor command so namespaces update with the file:

| Current file/project | New location | Why |
|---|---|---|
| `Core.Contracts/Enums/AssetEnums.cs` | `Core/Enums/AssetEnums.cs` | Status and category are business vocabulary |
| `Infrastructure.Contracts/Entities/HardwareAsset.cs` | `Core/Entities/HardwareAsset.cs` | Asset, assignment, and custodian are business entities |
| `Infrastructure.Contracts/IAssetRepository.cs` | `Application/Repositories/IAssetRepository.cs` | Application declares the persistence operation it needs |
| `Infrastructure.Contracts/ICustodianRepository.cs` | `Application/Repositories/ICustodianRepository.cs` | Same dependency rule |
| `Core.Contracts/Interfaces/IAssetService.cs` | `Application/Services/IAssetService.cs` | API calls an application use case contract |
| `Core/Services/AssetService.cs` | `Application/Services/AssetService.cs` | This class coordinates repositories and publishes events; it is an application service |
| `Core.Contracts/DTOs/AssetDtos.cs` | `Application/DTOs` for the asset use-case request/result types used in this guide | The old DTOs are part of the service contract, not Core business entities |
| `Core.Contracts/Messages` | Common.Messaging or remove if no external caller uses it | Broker contracts must not define business rules |

Update namespaces and build after each group. After every reference to `Core.Contracts` and `Infrastructure.Contracts` is gone, remove those projects from the solution. Do not delete the old directories until the code builds and the new migration preserves data.

When references are gone, remove the old projects from `.slnx`:

```powershell
dotnet sln .\Itam.AssetManagement.slnx remove `
  .\src\Services\AssetService\Itam.AssetService.Core.Contracts\Itam.AssetService.Core.Contracts.csproj

dotnet sln .\Itam.AssetManagement.slnx remove `
  .\src\Services\AssetService\Itam.AssetService.Infrastructure.Contracts\Itam.AssetService.Infrastructure.Contracts.csproj
```

### Step 3.5 — Centralize package versions like Base and Preschool

The reference repos use central package management; this repository currently pins versions in individual project files. Add `Directory.Packages.props` at the solution root and move each package version out of `.csproj` files into it. Keep all EF Core packages on the same compatible version. Use the versions already present in this repository when migrating; do not mix EF major versions.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.12" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.12" />
    <PackageVersion Include="System.IdentityModel.Tokens.Jwt" Version="8.23.0" />
  </ItemGroup>
</Project>
```

This is the relevant package subset. Add every other package already used by the solution (for example Swashbuckle and MassTransit) to the central file with the version currently in its project file, then remove `Version="..."` from each project-level `PackageReference`.

Put `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Tools`, and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` references in Infrastructure. Put `Microsoft.AspNetCore.Authentication.JwtBearer` in Shell. Put `System.IdentityModel.Tokens.Jwt` in the project that creates tokens (Infrastructure in this guide). Keep RabbitMQ/MassTransit in the infrastructure adapter or existing Common.Messaging project.

Then restore and build:

```powershell
dotnet restore .\Itam.AssetManagement.slnx
dotnet build .\Itam.AssetManagement.slnx
```

---

## 4. Build Core: entities and asset rules

Create folders `Entities`, `Enums`, and `Errors` in `Itam.AssetService.Core`.

### Step 4.1 — Move the enums and keep their stored values stable

```csharp
namespace Itam.AssetService.Core.Enums;

public enum AssetStatus
{
    InStock = 1,
    Assigned = 2,
    InRepair = 3,
    Retired = 4
}

public enum AssetCategory
{
    Laptop = 1,
    Desktop = 2,
    Monitor = 3,
    MobileDevice = 4,
    NetworkEquipment = 5
}

public enum AssetRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum IssueReportStatus
{
    Open = 1,
    UnderReview = 2,
    Repairing = 3,
    Resolved = 4,
    Rejected = 5
}

public enum AssetRepairStatus
{
    Open = 1,
    Completed = 2
}
```

The current enum is named `Instock`; if values are stored as integers, preserve the numeric values while renaming it to `InStock`. Do not reorder or renumber saved enum values.

### Step 4.2 — Move HardwareAsset into Core and guard state changes

Keep the fields already in `HardwareAsset.cs`. Add methods for transitions so controllers and repositories cannot set status without checking the rule.

```csharp
using Itam.AssetService.Core.Enums;
using Itam.AssetService.Core.Errors;
using Itam.Common.Base.Entities;

namespace Itam.AssetService.Core.Entities;

public sealed class HardwareAsset : AuditableEntity<Guid>
{
    public string AssetTag { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public AssetCategory Category { get; set; } = AssetCategory.Laptop;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal PurchaseCost { get; set; }
    public DateTime PurchaseDateUtc { get; set; }
    public DateTime WarrantyExpiryUtc { get; set; }
    public AssetStatus Status { get; private set; } = AssetStatus.InStock;
    public string? RetirementReason { get; private set; }
    public DateTime? RetiredAtUtc { get; private set; }
    public Guid? RetiredByUserId { get; private set; }

    public Guid? CurrentCustodianId { get; private set; }
    public Custodian? CurrentCustodian { get; set; }
    public DateTime? LastAssignedDateUtc { get; private set; }
    public ICollection<AssetAssignment> AssignmentHistory { get; set; } = new List<AssetAssignment>();

    public void AssignTo(Guid custodianId)
    {
        if (Status != AssetStatus.InStock)
            throw new AssetRuleException("Only an in-stock asset can be assigned.");

        CurrentCustodianId = custodianId;
        LastAssignedDateUtc = DateTime.UtcNow;
        Status = AssetStatus.Assigned;
    }

    public void ReturnToStock()
    {
        if (Status != AssetStatus.Assigned)
            throw new AssetRuleException("Only an assigned asset can be returned.");

        CurrentCustodianId = null;
        Status = AssetStatus.InStock;
    }

    public void StartRepair()
    {
        if (Status is AssetStatus.InRepair or AssetStatus.Retired)
            throw new AssetRuleException("This asset cannot enter repair from its current state.");

        // Keep the custodian while repair is open so the owner can see the status.
        Status = AssetStatus.InRepair;
    }

    public void CompleteRepair()
    {
        if (Status != AssetStatus.InRepair)
            throw new AssetRuleException("Only an asset in repair can complete repair.");

        Status = CurrentCustodianId.HasValue ? AssetStatus.Assigned : AssetStatus.InStock;
    }

    public void TransferTo(Guid newCustodianId)
    {
        if (Status != AssetStatus.Assigned)
            throw new AssetRuleException("Only an assigned asset can be transferred.");
        if (newCustodianId == Guid.Empty || newCustodianId == CurrentCustodianId)
            throw new AssetRuleException("Choose a different valid Custodian for the transfer.");

        CurrentCustodianId = newCustodianId;
        LastAssignedDateUtc = DateTime.UtcNow;
    }

    public void Retire(Guid adminId, string reason)
    {
        if (Status == AssetStatus.Retired)
            throw new AssetRuleException("This asset is already retired.");
        if (Status is AssetStatus.Assigned or AssetStatus.InRepair)
            throw new AssetRuleException("Return the asset and close repair before retirement.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new AssetRuleException("A retirement reason is required.");

        Status = AssetStatus.Retired;
        RetirementReason = reason.Trim();
        RetiredAtUtc = DateTime.UtcNow;
        RetiredByUserId = adminId;
    }
}
```

`AuditableEntity<Guid>` already gives this project `Id`, audit fields, soft-delete, and `RowVersion`. This is why Core may reference Common.Base. The asset methods contain business rules; Infrastructure will map these properties to SQL.

Create `Errors/AssetRuleException.cs`:

```csharp
namespace Itam.AssetService.Core.Errors;

public sealed class AssetRuleException(string message) : Exception(message) { }
```

Application catches this exception and converts it into a safe conflict result. Never return a stack trace to the API caller.

### Step 4.3 — Keep the current Custodian and assignment history

Keep `Custodian` and `AssetAssignment` as Core entities. `AssetAssignment` is one assignment period. Returning sets `ReturnedDateUtc`; assigning later creates another row. A transfer closes the old assignment and creates the new one in a transaction.

```csharp
public sealed class AssetAssignment : AuditableEntity<Guid>
{
    public Guid AssetId { get; set; }
    public HardwareAsset Asset { get; set; } = null!;
    public Guid CustodianId { get; set; }
    public Custodian Custodian { get; set; } = null!;
    public DateTime AssignedDateUtc { get; set; }
    public DateTime? ReturnedDateUtc { get; set; }
    public string? ConditionNotes { get; set; }
    public string AssignedBy { get; set; } = string.Empty;
    public string? ReturnedBy { get; set; }
}

public sealed class Custodian : AuditableEntity<Guid>
{
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    // Links an employee record to an Identity login. Null means not linked yet.
    public Guid? IdentityUserId { get; private set; }
    public ICollection<AssetAssignment> Assignments { get; set; } = new List<AssetAssignment>();

    public void LinkToUser(Guid identityUserId)
    {
        if (IdentityUserId.HasValue && IdentityUserId.Value != identityUserId)
            throw new InvalidOperationException("This Custodian is already linked to another login.");
        IdentityUserId = identityUserId;
    }
}
```

The Identity ID is stored on Custodian, not in the old assignment rows. This preserves existing `CustodianId` relationships and lets the API query a User’s assets by joining through the linked employee profile.

### Step 4.4 — Add request, issue, and repair entities

`AssetRequest` belongs to the Identity User who submitted it. `AssetIssueReport` records what the User reported. `AssetRepair` records what Admin did. Keep these as separate records so new requests never overwrite old decisions or repair history.

```csharp
public sealed class AssetRequest : AuditableEntity<Guid>
{
    public Guid RequestedByUserId { get; private set; }
    public AssetCategory RequestedCategory { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public AssetRequestStatus Status { get; private set; } = AssetRequestStatus.Pending;
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }
    public Guid? FulfilledAssignmentId { get; private set; }

    private AssetRequest() { } // EF Core uses this when it reads a saved row.

    public static AssetRequest Submit(Guid userId, AssetCategory category, string reason)
    {
        if (userId == Guid.Empty)
            throw new AssetRuleException("A signed-in user is required to request an asset.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new AssetRuleException("A reason is required for an asset request.");

        return new AssetRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = userId,
            RequestedCategory = category,
            Reason = reason.Trim()
        };
    }

    public void Approve(Guid adminId, string? note)
    {
        EnsurePending();
        Status = AssetRequestStatus.Approved;
        ReviewedByUserId = adminId;
        ReviewedAtUtc = DateTime.UtcNow;
        DecisionNote = note;
    }

    public void Reject(Guid adminId, string note)
    {
        EnsurePending();
        Status = AssetRequestStatus.Rejected;
        ReviewedByUserId = adminId;
        ReviewedAtUtc = DateTime.UtcNow;
        DecisionNote = note;
    }

    public void MarkFulfilled(Guid assignmentId)
    {
        if (Status != AssetRequestStatus.Approved)
            throw new AssetRuleException("Only an approved request can be fulfilled.");
        if (FulfilledAssignmentId.HasValue)
            throw new AssetRuleException("This request has already been fulfilled.");
        FulfilledAssignmentId = assignmentId;
    }

    private void EnsurePending()
    {
        if (Status != AssetRequestStatus.Pending)
            throw new AssetRuleException("Only a pending request can be reviewed.");
    }
}

public sealed class AssetIssueReport : AuditableEntity<Guid>
{
    public Guid AssetId { get; set; }
    public HardwareAsset Asset { get; set; } = null!;
    public Guid ReportedByUserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public IssueReportStatus Status { get; private set; } = IssueReportStatus.Open;
    public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public void StartRepair()
    {
        if (Status is not (IssueReportStatus.Open or IssueReportStatus.UnderReview))
            throw new AssetRuleException("This report cannot start repair from its current status.");
        Status = IssueReportStatus.Repairing;
    }

    public void Resolve(string note)
    {
        if (Status != IssueReportStatus.Repairing)
            throw new AssetRuleException("Only a report under repair can be resolved.");
        Status = IssueReportStatus.Resolved;
        ResolutionNote = note;
        ResolvedAtUtc = DateTime.UtcNow;
    }
}

public sealed class AssetRepair : AuditableEntity<Guid>
{
    public Guid AssetId { get; set; }
    public Guid? IssueReportId { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public Guid StartedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public AssetRepairStatus Status { get; private set; } = AssetRepairStatus.Open;

    public void Complete(string note)
    {
        if (Status != AssetRepairStatus.Open)
            throw new AssetRuleException("Only an open repair can be completed.");
        if (string.IsNullOrWhiteSpace(note))
            throw new AssetRuleException("A resolution note is required.");

        ResolutionNote = note.Trim();
        CompletedAtUtc = DateTime.UtcNow;
        Status = AssetRepairStatus.Completed;
    }
}
```

These are starter shapes. Add validation for required reason/description and reasonable string lengths in Application and EF configuration.

---

## 5. Build Application: use cases and interfaces

Application owns interfaces that business workflows need. Infrastructure supplies their implementations. This is the dependency inversion pattern: the inside layer asks for a capability; the outside layer chooses how to provide it.

Create folders:

```text
Itam.AssetService.Application/
  DTOs/
  Repositories/
  Services/
  Identity/
  DependencyInjection.cs
```

### Step 5.1 — Move repository contracts into Application

Create `Repositories/IAssetRepository.cs`:

```csharp
using Itam.AssetService.Core.Entities;
using Itam.Common.Base.Common;

namespace Itam.AssetService.Application.Repositories;

public interface IAssetRepository
{
    Task<HardwareAsset?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<AssetAssignment?> GetActiveAssignmentAsync(Guid assetId, CancellationToken ct);
    Task<bool> ExistsByTagAsync(string tag, CancellationToken ct);
    Task<bool> ExistsBySerialNumberAsync(string serialNumber, CancellationToken ct);
    Task AddAsync(HardwareAsset asset, CancellationToken ct);
    Task AddAssignmentHistoryAsync(AssetAssignment assignment, CancellationToken ct);
    Task<PagedResult<HardwareAsset>> GetPagedAsync(
        int page, int size, string? status, CancellationToken ct);
    Task<PagedResult<HardwareAsset>> GetAssignedToUserAsync(
        Guid identityUserId, int page, int size, CancellationToken ct);
    Task<bool> IsAssignedToUserAsync(Guid assetId, Guid identityUserId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

Create `Repositories/ICustodianRepository.cs`:

```csharp
using Itam.AssetService.Core.Entities;

namespace Itam.AssetService.Application.Repositories;

public interface ICustodianRepository
{
    Task<Custodian?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Custodian?> GetByIdentityUserIdAsync(Guid userId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

The `GetAssignedToUserAsync` implementation must join through `Custodian.IdentityUserId`. This is the server-side data boundary that prevents a User from seeing another employee’s device.

### Step 5.2 — Define the service contract and DTOs

Move `IAssetService` into Application. Keep request/response DTOs out of Core entities. To keep the first implementation easy to follow, this guide uses Application DTOs directly as controller input/output. If an HTTP payload later needs a different shape, add a separate DTO in API and map it to an Application command there:

```csharp
using Itam.AssetService.Application.DTOs;
using Itam.Common.Base.Common;

namespace Itam.AssetService.Application.Services;

public interface IAssetService
{
    Task<Result<AssetResponseDto>> CreateAssetAsync(CreateAssetRequestDto request, string actor, CancellationToken ct);
    Task<Result<AssetResponseDto>> UpdateAssetAsync(Guid assetId, UpdateAssetRequestDto request, CancellationToken ct);
    Task<Result<AssetResponseDto>> AssignAssetAsync(AssignAssetRequestDto request, string actor, CancellationToken ct);
    Task<Result<AssetResponseDto>> ReturnAssetAsync(Guid assetId, string actor, CancellationToken ct);
    Task<Result<AssetResponseDto>> TransferAsync(Guid assetId, Guid newCustodianId, string note, CancellationToken ct);
    Task<Result<bool>> RetireAsync(Guid assetId, string reason, CancellationToken ct);
    Task<Result<AssetResponseDto>> GetByIdAsync(Guid assetId, CancellationToken ct);
    Task<PagedResult<AssetResponseDto>> GetMyAssetsAsync(int page, int size, CancellationToken ct);
    Task<PagedResult<AssetResponseDto>> SearchAllAsync(int page, int size, string? status, CancellationToken ct);
}
```

Create `Application/DTOs/AssetDtos.cs`. Keep all the current HardwareAsset fields in the create request:

```csharp
using System.ComponentModel.DataAnnotations;
using Itam.AssetService.Core.Entities;
using Itam.AssetService.Core.Enums;

namespace Itam.AssetService.Application.DTOs;

public sealed record CreateAssetRequestDto(
    [Required, MaxLength(50)] string AssetTag,
    [Required, MaxLength(100)] string SerialNumber,
    AssetCategory Category,
    [Required, MaxLength(100)] string Make,
    [Required, MaxLength(100)] string Model,
    [Range(0, 50000)] decimal PurchaseCost,
    DateTime PurchaseDateUtc, DateTime WarrantyExpiryUtc);

public sealed record AssignAssetRequestDto(
    [Required] Guid AssetId,
    [Required] Guid CustodianId,
    [MaxLength(500)] string? ConditionNotes);

public sealed record UpdateAssetRequestDto(
    [Required, MaxLength(50)] string AssetTag,
    [Required, MaxLength(100)] string SerialNumber,
    AssetCategory Category,
    [Required, MaxLength(100)] string Make,
    [Required, MaxLength(100)] string Model,
    [Range(0, 50000)] decimal PurchaseCost,
    DateTime PurchaseDateUtc, DateTime WarrantyExpiryUtc);

public sealed record AssetResponseDto(
    Guid Id, string AssetTag, string SerialNumber, string Category,
    string Make, string Model, decimal PurchaseCost, string Status,
    Guid? CurrentCustodianId, string? CurrentCustodianName,
    DateTime? AssignedDateUtc, DateTime CreatedAtUtc)
{
    public static AssetResponseDto From(HardwareAsset asset, Custodian? custodian = null) => new(
        asset.Id, asset.AssetTag, asset.SerialNumber, asset.Category.ToString(),
        asset.Make, asset.Model, asset.PurchaseCost, asset.Status.ToString(),
        asset.CurrentCustodianId, custodian?.FullName,
        asset.LastAssignedDateUtc, asset.CreatedAtUtc);
}
```

`Result<T>` and `PagedResult<T>` are already in Common.Base. Do not put EF entities directly in a JSON response; the DTO prevents navigation properties and internal database fields from leaking.

### Step 5.3 — Add the current-user abstraction

Application needs to know who called a User-owned use case, but it should not reference `HttpContext`. Define:

```csharp
namespace Itam.AssetService.Application.Identity;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
}
```

Shell implements this interface using `IHttpContextAccessor` and validated claims. A client-supplied `userId` is never proof of ownership.

Create the implementation in Shell so Application remains independent of ASP.NET:

```csharp
using System.Security.Claims;
using Itam.AssetService.Application.Identity;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAdmin => Principal?.IsInRole("Admin") == true;
}
```

Register it in Shell with `AddHttpContextAccessor()` and `AddScoped<ICurrentUser, HttpCurrentUser>()`. In `GetMyAssetsAsync`, require `UserId` and call `GetAssignedToUserAsync`. In `GetByIdAsync`, an Admin can use the regular lookup; a User gets a result only when `asset.CurrentCustodian.IdentityUserId == currentUser.UserId`.

Also define the event publishing port in Application if current RabbitMQ events are still used:

```csharp
public interface IAssetEventPublisher
{
    Task AssetAssignedAsync(
        HardwareAsset asset, Custodian custodian, string actor, CancellationToken ct);
}
```

Infrastructure implements this with MassTransit. This keeps `IPublishEndpoint` out of Core and Application.

### Step 5.4 — Implement one existing workflow as an example

In Application, implement `AssetService.AssignAssetAsync`. This is the central method from the service; implement the other methods declared by `IAssetService` using the same load → validate → domain method → save pattern:

The following is the implementation body for the assignment use case. In your final file, keep it inside the `AssetService : IAssetService` class and add the other interface methods before building.

```csharp
using Itam.AssetService.Application.DTOs;
using Itam.AssetService.Application.Identity;
using Itam.AssetService.Application.Repositories;
using Itam.AssetService.Core.Errors;
using Itam.AssetService.Core.Entities;
using Itam.Common.Base.Common;
using Microsoft.Extensions.Logging;

public sealed class AssetService(
    IAssetRepository assets,
    ICustodianRepository custodians,
    ICurrentUser currentUser,
    IAssetEventPublisher events,
    ILogger<AssetService> logger) : IAssetService
{
    public async Task<Result<AssetResponseDto>> AssignAssetAsync(
        AssignAssetRequestDto request, string actor, CancellationToken ct)
    {
        if (!currentUser.IsAdmin)
            return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");

        var asset = await assets.GetByIdAsync(request.AssetId, ct);
        if (asset is null)
            return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");

        var custodian = await custodians.GetByIdAsync(request.CustodianId, ct);
        if (custodian is null)
            return Result<AssetResponseDto>.Failure("Custodian not found.", "CUSTODIAN_NOT_FOUND");

        try
        {
            asset.AssignTo(custodian.Id);
        }
        catch (AssetRuleException exception)
        {
            return Result<AssetResponseDto>.Failure(exception.Message, "INVALID_STATUS");
        }

        var assignment = new AssetAssignment
        {
            Id = Guid.NewGuid(),
            AssetId = asset.Id,
            CustodianId = custodian.Id,
            AssignedDateUtc = DateTime.UtcNow,
            AssignedBy = actor
        };

        await assets.AddAssignmentHistoryAsync(assignment, ct);
        await assets.SaveChangesAsync(ct);

        await events.AssetAssignedAsync(asset, custodian, actor, ct);
        logger.LogInformation("Asset {AssetTag} assigned by {Actor}", asset.AssetTag, actor);

        return Result<AssetResponseDto>.Success(AssetResponseDto.From(asset, custodian));
    }

    // Implement the other interface methods using the same rules:
    // load -> validate -> call domain method -> save -> return DTO.
}
```

The next methods are additional members of the same class. Paste them before the class’s closing brace in the previous example and remove its placeholder comment. `actor` must come from the authenticated server-side principal, as in the controller example; never accept it from a request body:

```csharp
public async Task<Result<AssetResponseDto>> CreateAssetAsync(
    CreateAssetRequestDto request, string actor, CancellationToken ct)
{
    if (!currentUser.IsAdmin)
        return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");
    if (await assets.ExistsByTagAsync(request.AssetTag.Trim(), ct) ||
        await assets.ExistsBySerialNumberAsync(request.SerialNumber.Trim(), ct))
        return Result<AssetResponseDto>.Failure("Asset tag or serial number already exists.", "DUPLICATE_ASSET_IDENTIFIER");

    var asset = new HardwareAsset
    {
        Id = Guid.NewGuid(), AssetTag = request.AssetTag.Trim(),
        SerialNumber = request.SerialNumber.Trim(), Category = request.Category,
        Make = request.Make.Trim(), Model = request.Model.Trim(),
        PurchaseCost = request.PurchaseCost, PurchaseDateUtc = request.PurchaseDateUtc,
        WarrantyExpiryUtc = request.WarrantyExpiryUtc
    };
    await assets.AddAsync(asset, ct);
    await assets.SaveChangesAsync(ct);
    return Result<AssetResponseDto>.Success(AssetResponseDto.From(asset));
}

public async Task<Result<AssetResponseDto>> ReturnAssetAsync(
    Guid assetId, string actor, CancellationToken ct)
{
    if (!currentUser.IsAdmin)
        return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");
    var asset = await assets.GetByIdAsync(assetId, ct);
    var active = await assets.GetActiveAssignmentAsync(assetId, ct);
    if (asset is null || active is null)
        return Result<AssetResponseDto>.Failure("Active assignment not found.", "NOT_FOUND");

    try
    {
        asset.ReturnToStock();
        active.ReturnedDateUtc = DateTime.UtcNow;
        active.ReturnedBy = actor;
        await assets.SaveChangesAsync(ct);
        return Result<AssetResponseDto>.Success(AssetResponseDto.From(asset));
    }
    catch (AssetRuleException ex)
    {
        return Result<AssetResponseDto>.Failure(ex.Message, "INVALID_ASSET_STATE");
    }
}

public async Task<Result<AssetResponseDto>> UpdateAssetAsync(
    Guid assetId, UpdateAssetRequestDto request, CancellationToken ct)
{
    if (!currentUser.IsAdmin)
        return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");
    var asset = await assets.GetByIdAsync(assetId, ct);
    if (asset is null)
        return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");

    var tagChanged = !string.Equals(asset.AssetTag, request.AssetTag.Trim(), StringComparison.OrdinalIgnoreCase);
    if (tagChanged && await assets.ExistsByTagAsync(request.AssetTag.Trim(), ct))
        return Result<AssetResponseDto>.Failure("Asset tag already exists.", "DUPLICATE_TAG");
    var serialChanged = !string.Equals(asset.SerialNumber, request.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase);
    if (serialChanged && await assets.ExistsBySerialNumberAsync(request.SerialNumber.Trim(), ct))
        return Result<AssetResponseDto>.Failure("Serial number already exists.", "DUPLICATE_SERIAL_NUMBER");

    asset.AssetTag = request.AssetTag.Trim();
    asset.SerialNumber = request.SerialNumber.Trim();
    asset.Category = request.Category;
    asset.Make = request.Make.Trim();
    asset.Model = request.Model.Trim();
    asset.PurchaseCost = request.PurchaseCost;
    asset.PurchaseDateUtc = request.PurchaseDateUtc;
    asset.WarrantyExpiryUtc = request.WarrantyExpiryUtc;
    await assets.SaveChangesAsync(ct);
    return Result<AssetResponseDto>.Success(
        AssetResponseDto.From(asset, asset.CurrentCustodian));
}

public async Task<Result<AssetResponseDto>> GetByIdAsync(Guid assetId, CancellationToken ct)
{
    var asset = await assets.GetByIdAsync(assetId, ct);
    if (asset is null)
        return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");

    var isOwner = asset.CurrentCustodian?.IdentityUserId == currentUser.UserId;
    if (!currentUser.IsAdmin && !isOwner)
        return Result<AssetResponseDto>.Failure("Asset not found.", "NOT_FOUND");

    return Result<AssetResponseDto>.Success(
        AssetResponseDto.From(asset, asset.CurrentCustodian));
}

public async Task<PagedResult<AssetResponseDto>> GetMyAssetsAsync(
    int page, int size, CancellationToken ct)
{
    if (currentUser.UserId is not Guid userId)
        return new PagedResult<AssetResponseDto>(Array.Empty<AssetResponseDto>(), 0, page, size);

    var result = await assets.GetAssignedToUserAsync(userId, page, size, ct);
    var items = result.Items
        .Select(asset => AssetResponseDto.From(asset, asset.CurrentCustodian))
        .ToList();
    return new PagedResult<AssetResponseDto>(items, result.TotalCount, page, size);
}

public async Task<PagedResult<AssetResponseDto>> SearchAllAsync(
    int page, int size, string? status, CancellationToken ct)
{
    if (!currentUser.IsAdmin)
        return new PagedResult<AssetResponseDto>(Array.Empty<AssetResponseDto>(), 0, page, size);

    var result = await assets.GetPagedAsync(page, size, status, ct);
    var items = result.Items
        .Select(asset => AssetResponseDto.From(asset, asset.CurrentCustodian))
        .ToList();
    return new PagedResult<AssetResponseDto>(items, result.TotalCount, page, size);
}
```

The sample shows why layers exist: the service coordinates, Core enforces the state rule, the repository loads/saves, and the event interface describes a capability. Infrastructure decides how SQL and RabbitMQ work.

Define `IAssetEventPublisher` in Application if RabbitMQ is still required. Its Infrastructure implementation uses MassTransit. Do not put MassTransit types or HTTP message DTOs in Core.

---

## 6. Build Infrastructure: EF Core, SQL Server, Identity

Infrastructure is the only service project that knows how the database is stored. Create folders `Data`, `Configurations`, `Repositories`, `Identity`, and `Services`.

### Step 6.1 — Add Identity user and role

Create `Infrastructure/Identity/AssetUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;

namespace Itam.AssetService.Infrastructure.Identity;

public sealed class AssetUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
    public string? DisplayName { get; set; }
}

public sealed class AssetRole : IdentityRole<Guid> { }
```

Identity stores a password hash, not the original password. `Custodian` is employee information; `AssetUser` is a login. Link them through the nullable unique `Custodian.IdentityUserId` field added in Core. Admin verifies the employee number/email before linking.

### Step 6.2 — Make AssetDbContext include Identity tables

Replace the current `DbContext` base class with `IdentityDbContext` so asset data and Identity users/roles share the SQL Server database and migrations:

```csharp
using Itam.AssetService.Core.Entities;
using Itam.AssetService.Application.Identity;
using Itam.AssetService.Infrastructure.Identity;
using Itam.Common.Base.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Itam.AssetService.Infrastructure.Data;

public sealed class AssetDbContext(
    DbContextOptions<AssetDbContext> options,
    ICurrentUser currentUser)
    : IdentityDbContext<AssetUser, AssetRole, Guid>(options)
{
    public DbSet<HardwareAsset> HardwareAssets => Set<HardwareAsset>();
    public DbSet<Custodian> Custodians => Set<Custodian>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<AssetRequest> AssetRequests => Set<AssetRequest>();
    public DbSet<AssetIssueReport> AssetIssueReports => Set<AssetIssueReport>();
    public DbSet<AssetRepair> AssetRepairs => Set<AssetRepair>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Required for Identity mappings.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity<Guid>>())
        {
            if (entry.State == EntityState.Added)
                {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
                    entry.Entity.CreatedBy = currentUser.UserId?.ToString() ?? "SYSTEM";
                }
            else if (entry.State == EntityState.Modified)
                {
                entry.Entity.LastModifiedAtUtc = DateTime.UtcNow;
                    entry.Entity.LastModifiedBy = currentUser.UserId?.ToString() ?? "SYSTEM";
                }
        }

        return base.SaveChangesAsync(ct);
    }
}
```

Keep the `HardwareAssets` table name during the first migration if it already contains data. A class rename does not require a table rename.

### Step 6.3 — Map constraints and history

Keep the current `HardwareAssetConfiguration` and add a rowversion mapping. `AuditableEntity` already has a `RowVersion` property:

```csharp
builder.ToTable("HardwareAssets");
builder.HasKey(x => x.Id);
builder.Property(x => x.AssetTag).IsRequired().HasMaxLength(50);
builder.HasIndex(x => x.AssetTag).IsUnique();
builder.Property(x => x.SerialNumber).IsRequired().HasMaxLength(50);
builder.HasIndex(x => x.SerialNumber).IsUnique();
builder.Property(x => x.Make).IsRequired().HasMaxLength(100);
builder.Property(x => x.Model).IsRequired().HasMaxLength(100);
builder.Property(x => x.PurchaseCost).HasPrecision(18, 2);
builder.Property(x => x.RowVersion).IsRowVersion();
builder.HasQueryFilter(x => !x.IsDeleted);
```

For `AssetAssignment`, add a filtered unique index to prevent two active assignment rows for one asset:

```csharp
builder.HasIndex(x => x.AssetId)
    .IsUnique()
    .HasFilter("[ReturnedDateUtc] IS NULL");
```

For the Custodian link, configure a unique filtered index and foreign key in Infrastructure:

```csharp
builder.HasIndex(x => x.IdentityUserId)
    .IsUnique()
    .HasFilter("[IdentityUserId] IS NOT NULL");
builder.HasOne<AssetUser>()
    .WithMany()
    .HasForeignKey(x => x.IdentityUserId)
    .OnDelete(DeleteBehavior.Restrict);
```

In `AssetRequestConfiguration`, add restrictive foreign keys for requester/reviewer and the optional fulfilled assignment. Add the matching reporter, repair-actor, and retirement-actor relationships in their own configurations:

```csharp
builder.HasOne<AssetUser>().WithMany()
    .HasForeignKey(x => x.RequestedByUserId)
    .OnDelete(DeleteBehavior.Restrict);
builder.HasOne<AssetUser>().WithMany()
    .HasForeignKey(x => x.ReviewedByUserId)
    .OnDelete(DeleteBehavior.Restrict);
builder.HasOne<AssetAssignment>().WithMany()
    .HasForeignKey(x => x.FulfilledAssignmentId)
    .OnDelete(DeleteBehavior.Restrict);
```

Also map `AssetIssueReport.AssetId` and `AssetRepair.AssetId` to `HardwareAsset` with restrictive deletes. Map `AssetIssueReport.ReportedByUserId`, `AssetRepair.StartedByUserId`, and `HardwareAsset.RetiredByUserId` to `AssetUser` with restrictive deletes. User/employee account removal is a deactivation; the foreign keys protect history.

### Step 6.4 — Implement the repository in Infrastructure

The interface stays in Application. The implementation uses EF Core:

```csharp
public sealed class AssetRepository(AssetDbContext db) : IAssetRepository
{
    public Task<HardwareAsset?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.HardwareAssets
            .Include(x => x.CurrentCustodian)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<AssetAssignment?> GetActiveAssignmentAsync(Guid assetId, CancellationToken ct) =>
        db.AssetAssignments.FirstOrDefaultAsync(
            x => x.AssetId == assetId && x.ReturnedDateUtc == null, ct);

    public Task<bool> ExistsByTagAsync(string tag, CancellationToken ct) =>
        db.HardwareAssets.AnyAsync(x => x.AssetTag == tag, ct);

    public Task<bool> ExistsBySerialNumberAsync(string serialNumber, CancellationToken ct) =>
        db.HardwareAssets.AnyAsync(x => x.SerialNumber == serialNumber, ct);

    public async Task<PagedResult<HardwareAsset>> GetPagedAsync(
        int page, int size, string? status, CancellationToken ct)
    {
        var query = db.HardwareAssets
            .Include(x => x.CurrentCustodian)
            .AsQueryable();
        if (Enum.TryParse<AssetStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(x => x.Status == parsedStatus);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<HardwareAsset>(rows, total, page, size);
    }

    public async Task<PagedResult<HardwareAsset>> GetAssignedToUserAsync(
        Guid userId, int page, int size, CancellationToken ct)
    {
        var query = db.AssetAssignments
            .Where(x => x.ReturnedDateUtc == null && x.Custodian.IdentityUserId == userId);
        var total = await query.CountAsync(ct);
        var assignments = await query
            .AsNoTracking()
            .Include(x => x.Asset)
                .ThenInclude(x => x.CurrentCustodian)
            .OrderByDescending(x => x.AssignedDateUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        var assets = assignments.Select(x => x.Asset).ToList();

        return new PagedResult<HardwareAsset>(assets, total, page, size);
    }

    public Task<bool> IsAssignedToUserAsync(Guid assetId, Guid userId, CancellationToken ct) =>
        db.AssetAssignments.AnyAsync(x =>
            x.AssetId == assetId && x.ReturnedDateUtc == null &&
            x.Custodian.IdentityUserId == userId, ct);

    public async Task AddAsync(HardwareAsset asset, CancellationToken ct) =>
        await db.HardwareAssets.AddAsync(asset, ct);

    public async Task AddAssignmentHistoryAsync(AssetAssignment row, CancellationToken ct) =>
        await db.AssetAssignments.AddAsync(row, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
```

Include `using Itam.AssetService.Core.Enums;` and `using Itam.Common.Base.Common;` at the top of the file. The repository now implements every method shown in `IAssetRepository`.

The ownership filter is in the SQL query. Do not load all assets and filter them in memory. For Admin’s all-asset search, use a separate method that is only called by an Admin-protected use case.

### Step 6.5 — Register Infrastructure services

Add `Infrastructure/DependencyInjection.cs`:

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AssetDbContext>((provider, options) =>
        {
            var db = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseSqlServer(db.BizDb, sql =>
                sql.CommandTimeout(db.CommandTimeoutSeconds));
        });

        services.AddIdentityCore<AssetUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<AssetRole>()
            .AddEntityFrameworkStores<AssetDbContext>()
            .AddSignInManager();

        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<ICustodianRepository, CustodianRepository>();
        services.AddScoped<IUserAccountLookup, IdentityUserAccountLookup>();
        services.AddScoped<IAssetRequestRepository, AssetRequestRepository>();
        services.AddScoped<IIssueRepository, IssueRepository>();
        services.AddScoped<IAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IUserAdminService, IdentityUserAdminService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAssetEventPublisher, MassTransitAssetEventPublisher>();
        return services;
    }
}
```

If RabbitMQ is not configured in a local environment, register a no-op publisher for Development or keep the broker required and document how to start it. Do not silently swallow publish failures.

---

## 7. Add authentication and authorization

Authentication identifies the caller. Authorization checks what they can do. Use Identity for password/account management and JWT bearer tokens for the API.

### Step 7.1 — Configure JWT options

Create `JwtOptions` in Common.Configuration or Application (it must not depend on ASP.NET):

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    [Required] public string Issuer { get; set; } = string.Empty;
    [Required] public string Audience { get; set; } = string.Empty;
    [Required] public string SecretKey { get; set; } = string.Empty;
    [Range(5, 120)] public int ExpirationMinutes { get; set; } = 15;
}
```

The existing appsettings already has `Jwt:Issuer`, `Audience`, `SecretKey`, and `ExpirationMinutes`. Keep issuer/audience/lifetime as ordinary configuration, but move `SecretKey` to User Secrets locally and a secret manager in deployment.

### Step 7.2 — Create a token after a successful login

Infrastructure implements the token interface using Identity and JWT libraries:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public (string Token, DateTime ExpiresUtc) Create(
        Guid userId, string email, IReadOnlyCollection<string> roles)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwt.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, email),
            new(ClaimTypes.Email, email)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
```

The secret must be random and at least 32 bytes for this HMAC example. Never log or return it. Keep access tokens short-lived; refresh-token support can be a later requirement.

### Step 7.3 — Validate the token in Shell

The Shell reads the same issuer, audience, and key and validates every request:

```csharp
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? throw new InvalidOperationException("JWT options are missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
    });
```

If token creation and validation use different issuer/audience/key values, the API rejects the token. Use HTTPS so the bearer token is protected in transit.

### Step 7.4 — Register users and assign roles safely

Create these Application contracts in `Application/Identity`:

```csharp
using Itam.Common.Base.Common;

public sealed record RegisterUserRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AdminCreateUserRequest(
    string Email, string DisplayName, string InitialPassword, string Role);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc);

public interface IAuthenticationService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken ct);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
}

public interface IUserAdminService
{
    Task<PagedResult<UserSummaryDto>> GetUsersAsync(int page, int size, CancellationToken ct);
    Task<Result<Guid>> CreateUserAsync(AdminCreateUserRequest request);
    Task<Result<bool>> DeactivateUserAsync(Guid userId);
    Task<Result<bool>> SetRoleAsync(Guid userId, string role);
}

public sealed record UserSummaryDto(
    Guid Id, string Email, string? DisplayName, bool IsActive, IReadOnlyList<string> Roles);

public interface ITokenService
{
    (string Token, DateTime ExpiresUtc) Create(
        Guid userId, string email, IReadOnlyCollection<string> roles);
}
```

For `GET /api/admin/users`, query Identity users in Infrastructure, page them in SQL, then map only safe fields and role names. Never return `PasswordHash`, `SecurityStamp`, or other Identity internals:

```csharp
public async Task<PagedResult<UserSummaryDto>> GetUsersAsync(
    int page, int size, CancellationToken ct)
{
    if (!currentUser.IsAdmin)
        return new PagedResult<UserSummaryDto>(Array.Empty<UserSummaryDto>(), 0, page, size);

    var query = userManager.Users.OrderBy(user => user.Email);
    var total = await query.CountAsync(ct);
    var users = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
    var items = new List<UserSummaryDto>();
    foreach (var user in users)
    {
        var roles = await userManager.GetRolesAsync(user);
        items.Add(new UserSummaryDto(user.Id, user.Email ?? "", user.DisplayName,
            user.IsActive, roles.ToArray()));
    }
    return new PagedResult<UserSummaryDto>(items, total, page, size);
}
```

Infrastructure implements authentication through `UserManager<AssetUser>` and `SignInManager<AssetUser>`. `ITokenService` takes primitive values and role names, so Application does not need to reference the Infrastructure-only `AssetUser` type.

Registration must do this on the server:

1. Validate email/password and check for duplicate email.
2. Create the Identity user with `UserManager.CreateAsync(user, password)`.
3. Assign the fixed `User` role with `UserManager.AddToRoleAsync(user, "User")`.
4. Ignore/reject a `role` property from the public request body.
5. Return safe profile data, never the password hash/security stamp.

For registration, the key Identity calls are:

```csharp
var user = new AssetUser
{
    UserName = request.Email.Trim(),
    Email = request.Email.Trim(),
    DisplayName = request.DisplayName.Trim(),
    IsActive = true
};

var created = await userManager.CreateAsync(user, request.Password);
if (!created.Succeeded)
    return Result<AuthResponse>.Failure("Registration could not be completed.", "INVALID_REGISTRATION");

var roleResult = await userManager.AddToRoleAsync(user, "User");
if (!roleResult.Succeeded)
{
    await userManager.DeleteAsync(user); // Safe here: this new account has no business history.
    return Result<AuthResponse>.Failure("Default role could not be assigned.", "ROLE_ASSIGNMENT_FAILED");
}

var roles = await userManager.GetRolesAsync(user);
var issued = tokenService.Create(user.Id, user.Email!, roles.ToArray());
return Result<AuthResponse>.Success(new AuthResponse(issued.Token, issued.ExpiresUtc));
```

Login checks the account and lets Identity verify the password. Never compare plaintext passwords yourself:

```csharp
public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
{
    var user = await userManager.FindByEmailAsync(request.Email.Trim());
    if (user is null || !user.IsActive)
        return Result<AuthResponse>.Failure("Email or password is invalid.", "LOGIN_FAILED");

    var check = await signInManager.CheckPasswordSignInAsync(
        user, request.Password, lockoutOnFailure: true);
    if (!check.Succeeded)
        return Result<AuthResponse>.Failure("Email or password is invalid.", "LOGIN_FAILED");

    var roles = await userManager.GetRolesAsync(user);
    var issued = tokenService.Create(user.Id, user.Email!, roles.ToArray());
    return Result<AuthResponse>.Success(new AuthResponse(issued.Token, issued.ExpiresUtc));
}
```

This method belongs inside `IdentityAuthenticationService`, which implements the Application interface. Inject `UserManager<AssetUser>`, `SignInManager<AssetUser>`, and `ITokenService` into that Infrastructure class.

The public registration endpoint always assigns `User`. Admin account creation is a separate Admin-only operation and may choose one of the two allowed roles:

```csharp
public async Task<Result<Guid>> CreateUserAsync(AdminCreateUserRequest request)
{
    if (!currentUser.IsAdmin)
        return Result<Guid>.Failure("Admin role is required.", "FORBIDDEN");
    if (request.Role is not ("Admin" or "User"))
        return Result<Guid>.Failure("Role must be Admin or User.", "INVALID_ROLE");
    if (!await roleManager.RoleExistsAsync(request.Role))
        return Result<Guid>.Failure("Role has not been seeded.", "ROLE_NOT_FOUND");

    var user = new AssetUser
    {
        UserName = request.Email.Trim(), Email = request.Email.Trim(),
        DisplayName = request.DisplayName.Trim(), IsActive = true
    };
    var created = await userManager.CreateAsync(user, request.InitialPassword);
    if (!created.Succeeded)
        return Result<Guid>.Failure("Account could not be created.", "INVALID_ACCOUNT");

    var assigned = await userManager.AddToRoleAsync(user, request.Role);
    if (!assigned.Succeeded)
    {
        await userManager.DeleteAsync(user); // New account has no history yet.
        return Result<Guid>.Failure("Role could not be assigned.", "ROLE_ASSIGNMENT_FAILED");
    }
    return Result<Guid>.Success(user.Id);
}
```

Protect this method with the same Admin policy as the controller. The initial password is still hashed by Identity; never write a password or password hash directly to SQL. Return a safe user DTO and do not return the submitted password.

Place the methods in Infrastructure’s `IdentityUserAdminService`, which implements `IUserAdminService`. Inject `UserManager<AssetUser>`, `RoleManager<AssetRole>`, and `ICurrentUser`. Implement `SetRoleAsync` using the fixed role allow-list; before demoting an Admin, count active Admin accounts and refuse to remove the last one. Register `IUserAdminService` in Infrastructure.

Admin account management also uses Identity APIs. Deactivate accounts instead of deleting accounts that may appear in history:

```csharp
public async Task<Result<bool>> DeactivateUserAsync(Guid userId)
{
    if (!currentUser.IsAdmin)
        return Result<bool>.Failure("Admin role is required.", "FORBIDDEN");
    var user = await userManager.FindByIdAsync(userId.ToString());
    if (user is null)
        return Result<bool>.Failure("User not found.", "NOT_FOUND");

    user.IsActive = false;
    var updated = await userManager.UpdateAsync(user);
    if (!updated.Succeeded)
        return Result<bool>.Failure("User could not be deactivated.", "UPDATE_FAILED");

    return Result<bool>.Success(true);
}
```

For role changes, accept only the fixed names `Admin` and `User`, verify the target role exists, and prevent removing the last active Admin. Keep the action behind the `UserAdmin` policy. Existing JWTs expire on their own; if immediate revocation is required, add a revocation/security-stamp check as a separate requirement.

### Link a login to an existing Custodian

Legacy assignments point to a `Custodian`, while sign-in uses an `AssetUser`. After verifying the employee number/email, Admin links the two records. Keep `UserManager` in Infrastructure by exposing a small Application interface:

```csharp
public interface IUserAccountLookup
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken ct);
}

public interface ICustodianLinkService
{
    Task<Result<bool>> LinkUserAsync(Guid custodianId, Guid userId, CancellationToken ct);
}
```

Add `SaveChangesAsync` to `ICustodianRepository`. Then implement the use case in Application:

```csharp
public sealed class CustodianLinkService(
    ICustodianRepository custodians,
    IUserAccountLookup accounts,
    ICurrentUser currentUser) : ICustodianLinkService
{
    public async Task<Result<bool>> LinkUserAsync(
        Guid custodianId, Guid userId, CancellationToken ct)
    {
        if (!currentUser.IsAdmin)
            return Result<bool>.Failure("Admin role is required.", "FORBIDDEN");
        if (!await accounts.ExistsAsync(userId, ct))
            return Result<bool>.Failure("User not found.", "NOT_FOUND");

        var custodian = await custodians.GetByIdAsync(custodianId, ct);
        if (custodian is null)
            return Result<bool>.Failure("Custodian not found.", "NOT_FOUND");
        var existingLink = await custodians.GetByIdentityUserIdAsync(userId, ct);
        if (existingLink is not null && existingLink.Id != custodianId)
            return Result<bool>.Failure("This login is already linked to another Custodian.", "ALREADY_LINKED");

        try
        {
            custodian.LinkToUser(userId);
            await custodians.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message, "ALREADY_LINKED");
        }
    }
}
```

Infrastructure implements the lookup with `UserManager<AssetUser>`:

```csharp
public sealed class IdentityUserAccountLookup(UserManager<AssetUser> users) : IUserAccountLookup
{
    public async Task<bool> ExistsAsync(Guid userId, CancellationToken ct) =>
        await users.FindByIdAsync(userId.ToString()) is not null;
}
```

Register the lookup in Infrastructure and the link service in Application. The SQL unique index on `Custodian.IdentityUserId` remains the final guard if two Admins try to link the same login at once.

Seed the `User` role before registration is enabled. Login verifies the password with `SignInManager`, checks `IsActive`, reads role names, then calls `ITokenService.Create`. Admin endpoints can create/edit/deactivate accounts and change role. Deactivate accounts rather than hard-delete them; old assignments must retain their history.

Create an idempotent startup/seed method with `RoleManager<AssetRole>` to add `Admin` and `User` only if missing. Bootstrap the first Admin using a secret-protected local seed or deployment operation; never commit a default Admin password.

Role seeding example:

```csharp
public static async Task SeedRolesAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AssetRole>>();

    foreach (var name in new[] { "Admin", "User" })
    {
        if (await roleManager.RoleExistsAsync(name))
            continue;

        var result = await roleManager.CreateAsync(new AssetRole
        {
            Name = name,
            NormalizedName = name.ToUpperInvariant()
        });

        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not create role {name}.");
    }
}
```

Call this once at startup after the Identity schema exists. The method is idempotent, so rerunning it does not duplicate roles.

### Step 7.5 — Enforce role and ownership rules

Register policy in Shell:

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AssetAdmin", policy => policy.RequireRole("Admin"))
    .AddPolicy("UserAdmin", policy => policy.RequireRole("Admin"));
```

Use `[Authorize(Policy = "AssetAdmin")]` for asset CRUD, all-assets search, assignments, request review, transfers, repair, and retirement. Use `[Authorize]` for signed-in User workflows.

For `/api/assets/mine`, Application reads `ICurrentUser.UserId`, then calls the repository’s Custodian join query. For an issue report, check the asset ID and linked owner ID together in SQL before inserting. Never accept `UserId` from the browser as proof of ownership. Return `401` when not signed in, `403` when the role is not allowed, and avoid revealing another User’s records.

---

## 8. Implement the new workflows

### 8.1 Request an asset

1. User submits category and business reason.
2. Application takes `RequestedByUserId` from `ICurrentUser` and saves status `Pending`.
3. User can list only their own requests; Admin can list all pending requests.
4. Admin approves/rejects and saves reviewer/time/note.
5. Approval does not automatically allocate a device. Admin selects an `InStock` asset and assigns it.
6. Save approval and assignment together, and mark request fulfilled.

Start with a request-specific repository contract and a small request DTO. The caller’s ID is taken from the validated token through `ICurrentUser`; it is never accepted in the body:

```csharp
public sealed record SubmitAssetRequestDto(AssetCategory Category, string Reason);

public interface IAssetRequestRepository
{
    Task AddAsync(AssetRequest request, CancellationToken ct);
    Task<AssetRequest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PagedResult<AssetRequest>> GetForUserAsync(Guid userId, int page, int size, CancellationToken ct);
    Task<PagedResult<AssetRequest>> GetPendingAsync(int page, int size, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public sealed record AssetRequestResponseDto(
    Guid Id, string Category, string Reason, string Status,
    DateTime CreatedAtUtc, string? DecisionNote, DateTime? ReviewedAtUtc)
{
    public static AssetRequestResponseDto From(AssetRequest request) => new(
        request.Id, request.RequestedCategory.ToString(), request.Reason,
        request.Status.ToString(), request.CreatedAtUtc,
        request.DecisionNote, request.ReviewedAtUtc);
}

public interface IAssetRequestService
{
    Task<Result<Guid>> SubmitAsync(SubmitAssetRequestDto input, CancellationToken ct);
    Task<Result<bool>> ReviewAsync(Guid requestId, bool approve, string? note, CancellationToken ct);
    Task<Result<Guid>> FulfillAsync(Guid requestId, Guid assetId, CancellationToken ct);
    Task<PagedResult<AssetRequestResponseDto>> GetMineAsync(int page, int size, CancellationToken ct);
    Task<PagedResult<AssetRequestResponseDto>> GetPendingAsync(int page, int size, CancellationToken ct);
}
```

The Application service owns the use case. The sample includes submission and review; the same service can add owner-filtered list methods that call `GetForUserAsync` with `ICurrentUser.UserId`.

```csharp
public sealed class AssetRequestService(
    IAssetRequestRepository requests,
    IAssetRepository assets,
    ICustodianRepository custodians,
    ICurrentUser currentUser) : IAssetRequestService
{
    public async Task<PagedResult<AssetRequestResponseDto>> GetMineAsync(
        int page, int size, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return new PagedResult<AssetRequestResponseDto>(Array.Empty<AssetRequestResponseDto>(), 0, page, size);
        var rows = await requests.GetForUserAsync(userId, page, size, ct);
        var items = rows.Items.Select(AssetRequestResponseDto.From).ToList();
        return new PagedResult<AssetRequestResponseDto>(items, rows.TotalCount, page, size);
    }

    public async Task<PagedResult<AssetRequestResponseDto>> GetPendingAsync(
        int page, int size, CancellationToken ct)
    {
        if (!currentUser.IsAdmin)
            return new PagedResult<AssetRequestResponseDto>(Array.Empty<AssetRequestResponseDto>(), 0, page, size);
        var rows = await requests.GetPendingAsync(page, size, ct);
        var items = rows.Items.Select(AssetRequestResponseDto.From).ToList();
        return new PagedResult<AssetRequestResponseDto>(items, rows.TotalCount, page, size);
    }

    public async Task<Result<Guid>> SubmitAsync(SubmitAssetRequestDto input, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result<Guid>.Failure("Sign in before requesting an asset.", "UNAUTHENTICATED");

        try
        {
            var request = AssetRequest.Submit(userId, input.Category, input.Reason);
            await requests.AddAsync(request, ct);
            await requests.SaveChangesAsync(ct);
            return Result<Guid>.Success(request.Id);
        }
        catch (AssetRuleException ex)
        {
            return Result<Guid>.Failure(ex.Message, "INVALID_REQUEST");
        }
    }

    public async Task<Result<bool>> ReviewAsync(
        Guid requestId, bool approve, string? note, CancellationToken ct)
    {
        if (!currentUser.IsAdmin || currentUser.UserId is not Guid adminId)
            return Result<bool>.Failure("Admin role is required.", "FORBIDDEN");

        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return Result<bool>.Failure("Request not found.", "NOT_FOUND");

        try
        {
            if (approve) request.Approve(adminId, note);
            else request.Reject(adminId, note ?? "Request was rejected.");

            await requests.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (AssetRuleException ex)
        {
            return Result<bool>.Failure(ex.Message, "INVALID_REQUEST_STATE");
        }
    }

    public async Task<Result<Guid>> FulfillAsync(
        Guid requestId, Guid assetId, CancellationToken ct)
    {
        if (!currentUser.IsAdmin || currentUser.UserId is not Guid adminId)
            return Result<Guid>.Failure("Admin role is required.", "FORBIDDEN");
        var request = await requests.GetByIdAsync(requestId, ct);
        var asset = await assets.GetByIdAsync(assetId, ct);
        if (request is null || asset is null)
            return Result<Guid>.Failure("Request or asset not found.", "NOT_FOUND");
        var custodian = await custodians.GetByIdentityUserIdAsync(request.RequestedByUserId, ct);
        if (custodian is null)
            return Result<Guid>.Failure("Link this User to a Custodian before assigning an asset.", "CUSTODIAN_NOT_LINKED");

        try
        {
            asset.AssignTo(custodian.Id); // Fails unless the asset is in stock.
            var assignment = new AssetAssignment
            {
                Id = Guid.NewGuid(), AssetId = asset.Id, CustodianId = custodian.Id,
                AssignedDateUtc = DateTime.UtcNow, AssignedBy = adminId.ToString()
            };
            request.MarkFulfilled(assignment.Id); // Fails unless approved and not already fulfilled.
            await assets.AddAssignmentHistoryAsync(assignment, ct);
            await requests.SaveChangesAsync(ct); // Same scoped DbContext saves both aggregates.
            return Result<Guid>.Success(assignment.Id);
        }
        catch (AssetRuleException ex)
        {
            return Result<Guid>.Failure(ex.Message, "INVALID_ASSET_STATE");
        }
    }
}
```

Fulfillment is a separate Admin action. Load the approved request, the requested user’s linked Custodian, and an in-stock asset. Call `AssignTo`, add one `AssetAssignment`, call `MarkFulfilled`, then save once. Because both repositories use the same scoped `AssetDbContext`, EF Core wraps that single `SaveChangesAsync` in a transaction; the assignment and request update succeed or fail together. Add `RequestedByUserId` to the SQL query for a User’s request list, and do not expose the EF entity as the response DTO.

### 8.2 Report a problem and repair

1. User selects an asset they currently own and submits description.
2. Repository query verifies an open assignment where `Custodian.IdentityUserId` matches the caller.
3. Save `AssetIssueReport` as `Open`; never overwrite the original report.
4. Admin reviews; if repair is needed, create `AssetRepair`, set report to `Repairing`, and call `asset.StartRepair()`.
5. Admin completes repair with resolution note; call `asset.CompleteRepair()` and update report/repair in one database transaction.
6. Keep all history, including rejected reports and completed repair records.

Use one issue service for reporting and Admin repair actions. The repository interface should provide `GetByIdAsync`, `GetRepairByIdAsync`, `AddAsync`, `AddRepairAsync`, and `SaveChangesAsync`. The key security check when creating a report is:

```csharp
public interface IIssueRepository
{
    Task<AssetIssueReport?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<AssetRepair?> GetRepairByIdAsync(Guid id, CancellationToken ct);
    Task<PagedResult<AssetIssueReport>> GetForUserAsync(Guid userId, int page, int size, CancellationToken ct);
    Task<PagedResult<AssetIssueReport>> GetAllAsync(int page, int size, CancellationToken ct);
    Task AddAsync(AssetIssueReport issue, CancellationToken ct);
    Task AddRepairAsync(AssetRepair repair, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IAssetIssueService
{
    Task<Result<Guid>> ReportAsync(Guid assetId, string description, CancellationToken ct);
    Task<PagedResult<AssetIssueResponseDto>> GetMineAsync(int page, int size, CancellationToken ct);
    Task<PagedResult<AssetIssueResponseDto>> GetAllForAdminAsync(int page, int size, CancellationToken ct);
    Task<Result<Guid>> StartRepairAsync(Guid assetId, Guid issueId, CancellationToken ct);
    Task<Result<bool>> CompleteRepairAsync(Guid assetId, Guid issueId, Guid repairId, string note, CancellationToken ct);
}

public sealed record AssetIssueResponseDto(
    Guid Id, Guid AssetId, string Description, string Status,
    DateTime ReportedAtUtc, string? ResolutionNote, DateTime? ResolvedAtUtc)
{
    public static AssetIssueResponseDto From(AssetIssueReport issue) => new(
        issue.Id, issue.AssetId, issue.Description, issue.Status.ToString(),
        issue.ReportedAtUtc, issue.ResolutionNote, issue.ResolvedAtUtc);
}
```

```csharp
public sealed class AssetIssueService(
    IAssetRepository assets,
    IIssueRepository issues,
    ICurrentUser currentUser) : IAssetIssueService
{
public async Task<PagedResult<AssetIssueResponseDto>> GetMineAsync(
    int page, int size, CancellationToken ct)
{
    if (currentUser.UserId is not Guid userId)
        return new PagedResult<AssetIssueResponseDto>(Array.Empty<AssetIssueResponseDto>(), 0, page, size);
    var rows = await issues.GetForUserAsync(userId, page, size, ct);
    var items = rows.Items.Select(AssetIssueResponseDto.From).ToList();
    return new PagedResult<AssetIssueResponseDto>(items, rows.TotalCount, page, size);
}

public async Task<PagedResult<AssetIssueResponseDto>> GetAllForAdminAsync(
    int page, int size, CancellationToken ct)
{
    if (!currentUser.IsAdmin)
        return new PagedResult<AssetIssueResponseDto>(Array.Empty<AssetIssueResponseDto>(), 0, page, size);
    var rows = await issues.GetAllAsync(page, size, ct);
    var items = rows.Items.Select(AssetIssueResponseDto.From).ToList();
    return new PagedResult<AssetIssueResponseDto>(items, rows.TotalCount, page, size);
}

public async Task<Result<Guid>> ReportAsync(
    Guid assetId, string description, CancellationToken ct)
{
    if (currentUser.UserId is not Guid userId)
        return Result<Guid>.Failure("Sign in before reporting an asset.", "UNAUTHENTICATED");

    var isOwner = await assets.IsAssignedToUserAsync(assetId, userId, ct);
    if (!isOwner)
        return Result<Guid>.Failure("Asset not found.", "NOT_FOUND");

    if (string.IsNullOrWhiteSpace(description))
        return Result<Guid>.Failure("Describe the problem first.", "INVALID_REPORT");

    var report = new AssetIssueReport
    {
        Id = Guid.NewGuid(),
        AssetId = assetId,
        ReportedByUserId = userId,
        Description = description.Trim(),
        ReportedAtUtc = DateTime.UtcNow
    };
    await issues.AddAsync(report, ct);
    await issues.SaveChangesAsync(ct);
    return Result<Guid>.Success(report.Id);
}
}
```

For Admin repair, add these methods to `AssetIssueService`. The constructor above provides the same collaborators shown in the sample:

```csharp
public async Task<Result<Guid>> StartRepairAsync(
    Guid assetId, Guid issueId, CancellationToken ct)
{
    if (!currentUser.IsAdmin || currentUser.UserId is not Guid adminId)
        return Result<Guid>.Failure("Admin role is required.", "FORBIDDEN");
    var asset = await assets.GetByIdAsync(assetId, ct);
    var issue = await issues.GetByIdAsync(issueId, ct);
    if (asset is null || issue is null || issue.AssetId != assetId)
        return Result<Guid>.Failure("Asset or issue report not found.", "NOT_FOUND");

    try
    {
        asset.StartRepair();
        issue.StartRepair();
        var repair = new AssetRepair
        {
            Id = Guid.NewGuid(), AssetId = assetId, IssueReportId = issueId,
            StartedAtUtc = DateTime.UtcNow, StartedByUserId = adminId
        };
        await issues.AddRepairAsync(repair, ct);
        await issues.SaveChangesAsync(ct);
        return Result<Guid>.Success(repair.Id);
    }
    catch (AssetRuleException ex)
    {
        return Result<Guid>.Failure(ex.Message, "INVALID_ASSET_STATE");
    }
}

public async Task<Result<bool>> CompleteRepairAsync(
    Guid assetId, Guid issueId, Guid repairId, string note, CancellationToken ct)
{
    if (!currentUser.IsAdmin)
        return Result<bool>.Failure("Admin role is required.", "FORBIDDEN");
    var asset = await assets.GetByIdAsync(assetId, ct);
    var issue = await issues.GetByIdAsync(issueId, ct);
    var repair = await issues.GetRepairByIdAsync(repairId, ct);
    if (asset is null || issue is null || repair is null ||
        issue.AssetId != assetId || repair.AssetId != assetId || repair.IssueReportId != issueId)
        return Result<bool>.Failure("Asset, issue, or repair record not found.", "NOT_FOUND");

    try
    {
        repair.Complete(note);
        issue.Resolve(note);
        asset.CompleteRepair();
        await issues.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
    catch (AssetRuleException ex)
    {
        return Result<bool>.Failure(ex.Message, "INVALID_ASSET_STATE");
    }
}
```

`IIssueRepository` should expose those three read methods, `AddAsync`, `AddRepairAsync`, and `SaveChangesAsync`. Both repository implementations must use the same scoped `AssetDbContext` so the asset, issue, and repair state changes are committed in one database transaction. Add a filtered unique index if the business rule allows only one open repair per asset.

### 8.3 Transfer

Admin can transfer an asset only when assigned and not in repair/retired. In one transaction close the previous `AssetAssignment`, insert the new assignment, update `CurrentCustodianId`, and record Admin/time/notes. If any operation fails, rollback all of them. Keep both assignment rows.

Add `GetActiveAssignmentAsync(assetId)` to `IAssetRepository`. A focused application method looks like this:

```csharp
public async Task<Result<AssetResponseDto>> TransferAsync(
    Guid assetId, Guid newCustodianId, string note, CancellationToken ct)
{
    if (!currentUser.IsAdmin || currentUser.UserId is not Guid adminId)
        return Result<AssetResponseDto>.Failure("Admin role is required.", "FORBIDDEN");

    var asset = await assets.GetByIdAsync(assetId, ct);
    var nextCustodian = await custodians.GetByIdAsync(newCustodianId, ct);
    var activeAssignment = await assets.GetActiveAssignmentAsync(assetId, ct);
    if (asset is null || nextCustodian is null || activeAssignment is null)
        return Result<AssetResponseDto>.Failure("Asset or active assignment not found.", "NOT_FOUND");

    try
    {
        activeAssignment.ReturnedDateUtc = DateTime.UtcNow;
        activeAssignment.ReturnedBy = adminId.ToString();
        asset.TransferTo(nextCustodian.Id);

        var nextAssignment = new AssetAssignment
        {
            Id = Guid.NewGuid(), AssetId = asset.Id, CustodianId = nextCustodian.Id,
            AssignedDateUtc = DateTime.UtcNow, AssignedBy = adminId.ToString(),
            ConditionNotes = note
        };
        await assets.AddAssignmentHistoryAsync(nextAssignment, ct);
        await assets.SaveChangesAsync(ct); // One DbContext save: all changes are atomic.
        return Result<AssetResponseDto>.Success(AssetResponseDto.From(asset, nextCustodian));
    }
    catch (AssetRuleException ex)
    {
        return Result<AssetResponseDto>.Failure(ex.Message, "INVALID_ASSET_STATE");
    }
}
```

Add `ReturnedBy` to `AssetAssignment` if the existing schema has no return actor column. Also reject a transfer to the current Custodian inside `HardwareAsset.TransferTo`. The SQL filtered unique index on active assignments is the final protection against concurrent Admin requests.

### 8.4 Retire

Admin gives a retirement reason. Reject retirement while an assignment is active or repair is open. Call `asset.Retire(adminId, reason)`, save actor/time/reason, and leave the database row. Retired assets are hidden from normal “available to assign” searches but remain visible to Admin with a status filter.

The Core example stores retirement reason, time, and Admin ID on the asset. Call it only from the Admin use case:

```csharp
public async Task<Result<bool>> RetireAsync(Guid assetId, string reason, CancellationToken ct)
{
    if (!currentUser.IsAdmin || currentUser.UserId is not Guid adminId)
        return Result<bool>.Failure("Admin role is required.", "FORBIDDEN");

    var asset = await assets.GetByIdAsync(assetId, ct);
    if (asset is null)
        return Result<bool>.Failure("Asset not found.", "NOT_FOUND");
    if (await assets.GetActiveAssignmentAsync(assetId, ct) is not null)
        return Result<bool>.Failure("Return the asset before retirement.", "ACTIVE_ASSIGNMENT");

    try
    {
        asset.Retire(adminId, reason);
        await assets.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
    catch (AssetRuleException ex)
    {
        return Result<bool>.Failure(ex.Message, "INVALID_ASSET_STATE");
    }
}
```

The filtered query for available assets should require `Status == AssetStatus.InStock`; this prevents retired items from being assigned again.

### 8.5 Keep business changes and events consistent

The existing project publishes asset events. Keep event contracts separate from HTTP DTOs. If events are business-critical, use a transactional outbox: save the database change and an outbox row in the same transaction; a background publisher sends pending events and marks them delivered after broker confirmation. Consumers must handle duplicate messages. If no downstream service consumes RabbitMQ yet, preserve/document the contract and defer outbox complexity.

The current `ProcessMessageAsync` has a semicolon immediately after its `if`, so its body runs regardless of operation type. Fix that and test each supported message before retaining the endpoint. If no external caller uses `AssetMessagesController`, do not keep a generic HTTP message endpoint just because it exists.

---

## 9. Build Shell and API composition

### Step 9.1 — Register Application

Create `Application/DependencyInjection.cs`:

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAssetRequestService, AssetRequestService>();
        services.AddScoped<IAssetIssueService, AssetIssueService>();
        services.AddScoped<ICustodianLinkService, CustodianLinkService>();
        return services;
    }
}
```

This follows Base/Preschool: each layer owns its registrations; Shell does not need a long list of concrete classes.

### Step 9.2 — Keep API controllers thin

The API contains HTTP controllers and may contain HTTP-only request/response DTOs. It does not contain EF queries. This first controller uses the Application request DTO directly; add API DTOs and mapping when the HTTP contract needs to differ. Example:

```csharp
[ApiController]
[Route("api/assets")]
public sealed class AssetsController(IAssetService assets) : ControllerBase
{
    [Authorize(Policy = "AssetAdmin")]
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var result = await assets.SearchAllAsync(page, size, status, ct);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        var result = await assets.GetMyAssetsAsync(page, size, ct);
        return Ok(result);
    }

    [Authorize(Policy = "AssetAdmin")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateAssetRequestDto request, CancellationToken ct)
    {
        var result = await assets.CreateAssetAsync(request, User.Identity?.Name ?? "ADMIN", ct);
        if (!result.IsSuccess)
            return BadRequest(new { code = result.ErrorCode, error = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await assets.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound();
    }
}
```

Important: `GetByIdAsync` must enforce ownership inside Application/repository for a User. `[Authorize]` alone only means the caller is signed in; it does not authorize access to every asset ID.

Add an Admin controller for account management and request review. Add an Auth controller for public registration/login. Registration request DTO must not contain a role.

Use these routes as a first API map:

```text
POST /api/auth/register                 Anonymous; creates User role only
POST /api/auth/login                    Anonymous; returns bearer token
GET  /api/assets/mine                   User; filtered to caller
GET  /api/assets/{id}                   User-owned or Admin
POST /api/asset-requests                 User; request extra asset
GET  /api/asset-requests/mine            User; own requests only
GET  /api/issues/mine                    User; issue reports they submitted
POST /api/assets/{id}/issues             User; only currently assigned asset
GET  /api/assets                         Admin; search all assets
POST /api/assets                         Admin; create asset
PUT  /api/assets/{id}                    Admin; update asset
POST /api/assets/assign                  Admin; assign to Custodian
POST /api/assets/{id}/return             Admin; return to stock
POST /api/assets/{id}/transfer           Admin; close/open assignment records
POST /api/assets/{id}/issues/{issueId}/start-repair Admin; begin a repair
POST /api/assets/{id}/issues/{issueId}/repairs/{repairId}/complete Admin; finish a repair
POST /api/assets/{id}/retire             Admin; keep row/history
GET  /api/admin/users                    Admin; list users
POST /api/admin/users                    Admin; create user
PUT  /api/admin/users/{id}               Admin; edit/deactivate/change role
GET  /api/admin/asset-requests           Admin; review all requests
GET  /api/admin/issues                    Admin; view every issue report
POST /api/admin/asset-requests/{id}/review Admin; approve or reject
POST /api/admin/asset-requests/{id}/fulfill Admin; assign selected in-stock item
PUT  /api/admin/custodians/{id}/link-user Admin; link login to employee record
```

This route map uses the asset URL space for both roles; the authorization policy separates what Admin and User may do. The UI can hide Admin actions from Users, but every Admin action still requires a server-side policy.

Authentication controller example:

```csharp
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthenticationService authentication) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserRequest request, CancellationToken ct)
    {
        var result = await authentication.RegisterAsync(request, ct);
        return result.IsSuccess
            ? Ok(result.Data)
            : BadRequest(new { code = result.ErrorCode, error = result.ErrorMessage });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await authentication.LoginAsync(request, ct);
        return result.IsSuccess
            ? Ok(result.Data)
            : Unauthorized(new { code = result.ErrorCode, error = result.ErrorMessage });
    }
}
```

`[AllowAnonymous]` is only for registration and login. The Admin user controller uses `[Authorize(Policy = "UserAdmin")]` on the controller or its actions.

### Step 9.3 — Compose the application in Shell

Infrastructure registration shown in Step 6.5 configures `AssetDbContext` and Identity. Shell adds options, the layers, API controllers, JWT, policies, and middleware:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddApplicationPart(typeof(AssetsController).Assembly);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IT Asset Management API", Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer", BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// AddAuthentication().AddJwtBearer(...) using the validated JwtOptions.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AssetAdmin", p => p.RequireRole("Admin"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
```

Authentication must run before authorization. Add the full JWT validation code from Step 7.3 before `builder.Build()`.

---

## 10. Connect SQL Server and use SSMS

SSMS is the client; a SQL Server engine must be installed and running. Connect in SSMS first. Use that exact server name in the connection string. Common local names are `localhost`, `localhost\SQLEXPRESS`, and `(localdb)\MSSQLLocalDB`.

Initialize User Secrets for Shell:

```powershell
dotnet user-secrets init `
  --project .\src\Services\AssetService\Itam.AssetService.Shell\Itam.AssetService.Shell.csproj

dotnet user-secrets set "Database:BizDb" `
  "Server=localhost;Database=ItamAssetManagement;Trusted_Connection=True;TrustServerCertificate=True;" `
  --project .\src\Services\AssetService\Itam.AssetService.Shell\Itam.AssetService.Shell.csproj

dotnet user-secrets set "Jwt:SecretKey" "<random-secret-at-least-32-bytes>" `
  --project .\src\Services\AssetService\Itam.AssetService.Shell\Itam.AssetService.Shell.csproj
```

`TrustServerCertificate=True` is only for local development. Use a valid TLS certificate in production. If using SQL Authentication, keep the password in User Secrets, not `appsettings.json`.

Add an initial migration. Infrastructure owns the DbContext; Shell is the startup project because it provides configuration and DI:

```powershell
dotnet ef migrations add InitialAssetManagement `
  --project .\src\Services\AssetService\Itam.AssetService.Infrastructure `
  --startup-project .\src\Services\AssetService\Itam.AssetService.Shell `
  --context AssetDbContext `
  --output-dir Data\Migrations
```

For the existing database, do not blindly add a new initial migration. First compare the model to the current schema, back up data, and create a forward migration that adds Identity tables and the nullable Custodian link without dropping `HardwareAssets`, `Custodians`, or `AssetAssignments`.

Generate SQL for review in SSMS:

```powershell
New-Item -ItemType Directory -Force .\artifacts | Out-Null

dotnet ef migrations script --idempotent `
  --project .\src\Services\AssetService\Itam.AssetService.Infrastructure `
  --startup-project .\src\Services\AssetService\Itam.AssetService.Shell `
  --context AssetDbContext `
  --output .\artifacts\AssetManagement.sql
```

Open `artifacts\AssetManagement.sql` in SSMS, verify the selected server/database, review the script, then execute. Refresh Object Explorer and verify Identity tables, asset tables, indexes, and `__EFMigrationsHistory`. For a development-only direct update, use `dotnet ef database update` with the same `--project`, `--startup-project`, and `--context` arguments.

Never edit a migration that has already been applied to a shared database; add a new migration. Never run destructive database commands to fix a routine migration problem.

### Preserve existing Custodian and assignment data

1. Create Identity tables first.
2. Add nullable `Custodian.IdentityUserId` and a unique filtered index.
3. Keep existing Custodian IDs and assignment rows unchanged.
4. Admin verifies employee number/email and links login to Custodian.
5. User asset queries join active `AssetAssignment` → `Custodian.IdentityUserId`.
6. Test legacy devices with two different accounts before allowing User access.

This prevents old assignment history from being lost during authentication work.

---

## 11. Verify the refactor in Swagger/Postman

Build and launch Shell:

```powershell
dotnet build .\Itam.AssetManagement.slnx
dotnet run --project .\src\Services\AssetService\Itam.AssetService.Shell
```

Use the Swagger URL printed in the console. Create one Admin through a secure seed/bootstrap operation, register two test Users, then link each User to a different Custodian.

Example registration body (no role field):

```json
{
  "email": "user1@example.test",
  "password": "A-long-local-only-password!",
  "displayName": "Sample User"
}
```

Example asset request:

```json
{
  "requestedCategory": "Laptop",
  "reason": "I need a device for my assigned duties."
}
```

Example issue report:

```json
{
  "description": "The battery does not hold charge."
}
```

Check each case:

1. No token on `/api/assets/mine` returns `401`.
2. User A sees only assets assigned to A’s linked Custodian.
3. User B cannot read or report an issue on User A’s asset ID.
4. User cannot create/retire assets, manage users, approve requests, transfer, or repair; expect `403`.
5. Admin can view all assets, manage accounts, assign/return, review requests, transfer, repair, and retire.
6. Assigning an assigned asset, retiring an assigned asset, or transferring an asset in repair fails with a clear conflict and no partial history updates.
7. Transfer closes the previous assignment and creates one new active assignment.
8. A User registration payload containing `"role": "Admin"` is rejected or ignored; resulting account is User.

Do these manually as you implement. Also add automated unit tests for Core state rules, integration tests for SQL constraints, and API tests for cross-user denial. A hidden button in a front end is not authorization.

---

## 12. Common problems

| Problem | Likely cause | What to inspect |
|---|---|---|
| Solution cannot find Shell/API | Project is not in `.slnx` | `dotnet sln list` and add existing csproj files |
| `DatabaseOptions.BizDb` compile error | C# options type and JSON keys differ | Keep `Database:BizDb` and define matching `BizDb` property |
| EF cannot find `AssetDbContext` | Wrong project/startup args or missing Design package | Context in Infrastructure; startup project is Shell |
| SQL connection fails | SQL Server service/instance name or auth mismatch | Use server name that connects in SSMS; check User Secrets |
| Identity tables missing | Context does not inherit IdentityDbContext or lacks base mapping | Check `IdentityDbContext` and `base.OnModelCreating` |
| Token returns 401 | issuer/audience/key mismatch or expired token | Compare token creation and validation values |
| User can see another User’s asset | Query trusts caller-supplied UserId or filters after loading | Filter by `IdentityUserId` in SQL in the repository |
| Duplicate open assignments | No transaction or filtered unique index | Check transaction and SQL filtered index |
| Asset saves but RabbitMQ event is missing | SQL save and message publish are separate | Use an outbox if event delivery is required |
| Namespace errors after moving files | Old references still point to Contracts projects | Update namespaces/usings and project references layer by layer |

---

## 13. Implementation order and definition of done

Implement in this order. Build after each step and keep the old business behavior working:

1. Add API/Shell to solution; create Application; centralize package versions.
2. Move enums/entities into Core and repository/service interfaces into Application.
3. Implement Infrastructure repository interfaces and its DI registration.
4. Move use-case logic to Application; keep API controllers thin.
5. Update DatabaseOptions so JSON/C# names agree; connect SQL Server and inspect migration.
6. Add Identity tables, Admin/User roles, registration/login, and secure bootstrap Admin.
7. Add Custodian-to-Identity linking and prove User queries are owner-filtered.
8. Add asset requests, reports/repairs, transfers, and retirement one workflow at a time.
9. Preserve required RabbitMQ events; add an outbox if delivery must be reliable.
10. Remove the old Contracts projects only when no references remain.

Done means: the five-project service builds; existing asset registration/search/assignment/return behavior works; user data is owner-filtered in SQL; Admin-only operations are protected; history survives return/transfer/repair/retirement; and SQL changes are reviewed migrations.

---

## References

- Supplied `IT_Asset_Management_Microservice_Guide_NET10.docx` for the original Shell/API and messaging pattern.
- Base and Preschool source trees for layer organization and per-layer dependency registration.
- Microsoft Learn: [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0).
- Microsoft Learn: [Role-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0).
- Microsoft Learn: [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/).
- NuGet: [System.IdentityModel.Tokens.Jwt](https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt).

## Final note

Follow the **five-project target** at the start of this document. `Core.Contracts` and `Infrastructure.Contracts` are temporary current-state projects, not extra target layers. The easiest way to learn this architecture is to move one responsibility, build, and follow one request from API → Application → Core/Infrastructure → SQL before moving on.
