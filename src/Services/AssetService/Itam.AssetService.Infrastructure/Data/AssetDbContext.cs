

using Itam.AssetService.Application.Identity;
using Itam.AssetService.Core.Entities;
using Itam.AssetService.Infrastructure.Identity;
using Itam.Common.Base.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace Itam.AssetService.Infrastructure.Data
{
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
            //Automatically timestamp and audit all modified entities!
            foreach (var entry in ChangeTracker.Entries<AuditableEntity<Guid>>())
            {
                if(entry.State == EntityState.Added)
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
}
