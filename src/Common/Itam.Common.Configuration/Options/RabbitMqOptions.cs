
using System.ComponentModel.DataAnnotations;

namespace Itam.Common.Configuration.Options
{
    public class RabbitMqOptions
    {
        public const string SectionName = "RabbitMq";

        [Required]
        public string Host { get; set;  } = "localhost";
        
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";

        [Required]
        public string Username { get; set; } = "guest";

        [Required]
        public string Password { get; set; } = "guest";

        public int RetryCount { get; set; } = 3;
    }
}
