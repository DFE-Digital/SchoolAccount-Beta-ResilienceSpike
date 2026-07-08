using System.Text.RegularExpressions;
using Polly;

namespace SchoolAccount.ResiliencePlayground.Models;

public class Service
{
    public string ServiceName { get; }
    public string BaseUrl { get; }
    
    public ResiliencePipeline<HttpResponseMessage> Pipeline { get; set; }

    public List<ServiceState> History { get; init; } = [];

    public ServiceState CurrentState => History.Count > 0
        ? History.MaxBy(x => x.LastChecked) ?? throw new ApplicationException()
        : new ServiceState(ServiceStatus.Unknown,
            new ServicePerformance(0, TimeSpan.Zero, DateTime.UtcNow), []);

    public TimeSpan AverageResponseTime => History.Count > 0
        ? TimeSpan.FromMilliseconds(History.Average(x => x.Performance.Response.TotalMilliseconds))
        : TimeSpan.Zero;

    public Service(string name, string url, ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        ServiceName = name;
        BaseUrl = url;
        Pipeline = pipeline;
    }
}

public record ServiceState(
    DateTime LastChecked,
    ServiceStatus Status,
    ServicePerformance Performance,
    Error? Error = null,
    List<Log> Logs = null!)
{
    public ServiceState(ServiceStatus status, ServicePerformance performance, List<string> Logs)
        : this(DateTime.UtcNow, status, performance, Logs: Logs.Select(x => new Log(x)).ToList())
    {
    }

    public ServiceState(ServiceStatus status, string message, ServicePerformance performance, List<string> Logs)
        : this(DateTime.UtcNow, status, performance, new Error(message), Logs.Select(x => new Log(x)).ToList())
    {
    }
}

public record ServicePerformance(int? StatusCode, TimeSpan Response, DateTime Timestamp)
{
    public static ServicePerformance Create(int? statusCode, double responseTimeMs)
    {
        return new ServicePerformance(statusCode, TimeSpan.FromMilliseconds(responseTimeMs), DateTime.UtcNow);
    }
};

public enum ServiceStatus
{
    Unknown = 0,
    Healthy,
    Degraded,
    Error
}

public class Log
{
    public Log(string log)
    {
        var parsed = LogEnumParser.ParseLine(log);
        Type = parsed.Type;
        Message = parsed.Message;
    }

    public Log(LogType type, string message)
    {
        Type = type;
        Message = message;
    }

    public LogType Type { get; init; }
    public string Message { get; init; }
};

public static partial class LogEnumParser
{
    [GeneratedRegex(@"^\[(?<type>[^\]]+)\]\s*(?<msg>.*)$")]
    private static partial Regex Pattern();

    public static (LogType Type, string Message) ParseLine(string input)
    {
        var match = Pattern().Match(input);

        if (!match.Success)
        {
            return (LogType.Unknown, input);
        }

        var rawEnum = match.Groups["type"].Value;
        var message = match.Groups["msg"].Value.Trim();

        return !Enum.TryParse<LogType>(rawEnum, ignoreCase: true, out var parsedEnum) 
            ? (LogType.Unknown, input)
            : (parsedEnum, message);
    }
}

public enum LogType
{
    Unknown = 0,
    Start,
    Timeout,
    Retry,
    CircuitBreaker,
    Complete,
    Failed
}