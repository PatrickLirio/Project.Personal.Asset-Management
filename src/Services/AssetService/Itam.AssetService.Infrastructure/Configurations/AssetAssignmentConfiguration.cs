
using Itam.AssetService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itam.AssetService.Infrastructure.Configurations
{
    public class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
    {
        public void Configure(EntityTypeBuilder<AssetAssignment> builder)
        {
            builder.HasIndex(x => x.AssetId).IsUnique().HasFilter("[ReturnedDateUtc] IS NULL");
        }
    }
}