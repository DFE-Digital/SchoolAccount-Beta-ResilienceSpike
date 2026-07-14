using System.Text.RegularExpressions;
using Polly;

namespace SchoolAccount.ResiliencePlayground.Models;

public class Service
{
    public string ServiceName { get; }
    public string BaseUrl { get; }
    
    public ResiliencePipeline Pipeline { get; set; }

    public List<ServiceState> History { get; init; } = [];

    public ServiceState CurrentState(TrafficSource source) =>
        History.Where(x => x.Source == source).MaxBy(x => x.LastChecked)
        ?? new ServiceState(DateTime.UtcNow, source, ServiceStatus.Unknown,
            new ServicePerformance(0, TimeSpan.Zero, DateTime.UtcNow), Logs: []);

    public TimeSpan AverageResponseTime(TrafficSource source)
    {
        var relevant = History.Where(x => x.Source == source).ToList();
        return relevant.Count > 0
            ? TimeSpan.FromMilliseconds(relevant.Average(x => x.Performance.Response.TotalMilliseconds))
            : TimeSpan.Zero;
    }

    public Service(string name, string url, ResiliencePipeline pipeline)
    {
        ServiceName = name;
        BaseUrl = url;
        Pipeline = pipeline;
    }
}

public record ServiceState(
    DateTime LastChecked,
    TrafficSource Source,
    ServiceStatus Status,
    ServicePerformance Performance,
    Error? Error = null,
    List<Log> Logs = null!)
{
    public ServiceState(TrafficSource source, ServiceStatus status, ServicePerformance performance, List<string> logs)
        : this(DateTime.UtcNow, source, status, performance, Logs: logs.Select(x => new Log(x)).ToList())
    {
    }

    public ServiceState(TrafficSource source, ServiceStatus status, string message, ServicePerformance performance, List<string> logs)
        : this(DateTime.UtcNow, source, status, performance, new Error(message), logs.Select(x => new Log(x)).ToList())
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
    Failed,
    Chaos
}