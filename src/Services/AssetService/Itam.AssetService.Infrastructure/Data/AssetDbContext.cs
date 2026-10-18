
using Itam.AssetService.Infrastructure.Contracts.Entities;
using Itam.Common.Base.Entities;
using Microsoft.EntityFrameworkCore;

namespace Itam.AssetService.Infrastructure.Data
{
    public class AssetDbContext(DbContextOptions<AssetDbContext> options) : DbContext(options)
    {
        public DbSet<HardwareAsset> HardwareAssets => Set<HardwareAsset>();
        public DbSet<Custodian> Custodians => Set<Custodian>();
        public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetDbContext).Assembly);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            //Automatically timestamp and audit all modified entities!
            foreach (var entry in ChangeTracker.Entries<AuditableEntity<Guid>>())
            {
                if(entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAtUtc = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.LastModifiedAtUtc = DateTime.UtcNow;
                }
            }
            return base.SaveChangesAsync(cancellationToken);
        }


    }
}