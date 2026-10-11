
namespace Itam.Common.Base.Entities
{

    /// <summary>
    /// Base CLass for all entities with audit trails and soft delete capabilities.
    /// </summary>
    public abstract class AuditableEntity <Tkey>
    {
        public Tkey Id { get; set; } = default;

        //audit columns
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "SYSTEM";
        public DateTime? LastModifiedAtUtc { get; set; }
        public string? LastModifiedBy { get; set; }


        //Soft Delete flag
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAtUtc { get; set; }

        //Concurrency token: prevents two admins from overwriting each other's changes
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
