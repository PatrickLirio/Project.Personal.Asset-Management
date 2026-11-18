
using System.ComponentModel.DataAnnotations;

namespace Itam.Common.Configuration.Options
{
    public class DatabaseOptions
    {
    public const string SectionName = "Database";
 
    [Required(ErrorMessage = "Database connection string is required.")]
    public string BizDb { get; set; } = string.Empty;
 
    public string? AuditDb { get; set; }
    public string? TranDb { get; set; }
 
    [Range(5, 120)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    }
}
