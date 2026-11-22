

using Itam.AssetService.Core.Entities;
using Itam.AssetService.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itam.AssetService.Infrastructure.Configurations
{
    public class AssetRequestConfiguration :IEntityTypeConfiguration<AssetRequest>
    {
        public void Configure(EntityTypeBuilder<AssetRequest> builder)
        {
            builder.HasOne<AssetUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AssetUser>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<AssetAssignment>().WithMany().HasForeignKey(x => x.FulfilledAssignmentId).OnDelete(DeleteBehavior.Restrict);
        }
        
    }
}