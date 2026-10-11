

namespace Itam.Common.Messaging.Contracts
{
    public abstract class BaseMessage <TRequest, TResponse>
        where TRequest : class, new() 
        where TResponse : class, new()
    {
            public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
            public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
            public DateTime TimeStampUtc { get; set; } = DateTime.UtcNow;
            public string SourceSystem { get; set; } = "ITAMS_WEB_API";

        public TRequest RequestBody { get; set; } = new();
        public TResponse ResponseBody { get; set;} = new();
    }
}
