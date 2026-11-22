

using System.ComponentModel.DataAnnotations;

namespace Itam.Common.Configuration.Options
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";
        [Required] 
        public string Issuer {get; set; } = string.Empty;
        [Required]
        public string Audience { get; set; } = string.Empty;
        [Required]
        public string SecretKey { get; set; } = string.Empty;
        [Range(5, 120)]
        public int ExpirationMinutes { get; set;} = 15;

    }
}