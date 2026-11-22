

using Itam.AssetService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Itam.AssetService.Infrastructure.Configurations
{

    /// <summary>
    /// this part is the fluent api config of HardwareAsset Entity
    /// </summary>
    public class HardwareAssetConfiguration : IEntityTypeConfiguration<HardwareAsset>
    {
        public void Configure(EntityTypeBuilder<HardwareAsset> builder)
        {
            builder.ToTable("HardwareAssets");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.AssetTag).IsRequired().HasMaxLength(50);
            builder.HasIndex(a => a.AssetTag).IsUnique(); // unique index prevents duplicate tags

            builder.Property(a => a.SerialNumber).IsRequired().HasMaxLength(50);
            builder.HasIndex(a => a.SerialNumber).IsUnique();

            builder.Property(a => a.Make).IsRequired().HasMaxLength(100);
            builder.Property(a => a.Model).IsRequired().HasMaxLength(100);
            builder.Property(a => a.PurchaseCost).HasPrecision(18,2);

            builder.Property(a => a.RowVersion).IsRowVersion();

            //Soft-delete query filter: Automatically excldes deleted assets from all queries
            builder.HasQueryFilter(a => !a.IsDeleted);
        }
    }
}