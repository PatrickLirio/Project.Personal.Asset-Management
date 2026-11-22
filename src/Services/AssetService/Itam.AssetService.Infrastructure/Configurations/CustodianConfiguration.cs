

using Itam.AssetService.Core.Entities;
using Itam.AssetService.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itam.AssetService.Infrastructure.Configurations
{
    public class CustodianConfiguration : IEntityTypeConfiguration<Custodian>
    {
        public void Configure(EntityTypeBuilder<Custodian> builder)
        {
        builder.HasIndex(x => x.IdentityUserId).IsUnique().HasFilter("[IdentityUserId] IS NOT NULL");
        builder.HasOne<AssetUser>().WithMany().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.Restrict);
        }
        
    }
}