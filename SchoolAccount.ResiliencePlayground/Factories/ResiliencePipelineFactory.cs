using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Simmy;
using Polly.Simmy.Fault;
using Polly.Simmy.Latency;
using Polly.Timeout;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Factories;

public static class ResiliencePipelineFactory
{
    public static readonly ResiliencePropertyKey<List<string>> LogTraceKey = new("RequestLogTrace");

    public static ResiliencePipeline Monitor()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TaskCanceledException>()
                    .Handle<HttpRequestException>()
                    .HandleResult(RetryPredicateHandler),
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Constant,
                OnRetry = OnRetryLogger,
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(1),
                OnTimeout = OnTimeoutLogger
            })
            .Build();
    }

    public static ResiliencePipeline Create(
        ServiceManifest vendor,
        ResilienceSettings defaults,
        ChaosSettings chaos)
    {
        var maxRetries = vendor.MaxRetryAttempts
                         ?? defaults.MaxRetryAttempts
                         ?? throw new InvalidOperationException("Max retry attempts not set");
        var delaySec = vendor.DelaySeconds
                       ?? defaults.DelaySeconds
                       ?? throw new InvalidOperationException("Delay seconds not set");
        var timeoutSec = vendor.TimeoutSeconds
                         ?? defaults.TimeoutSeconds
                         ?? throw new InvalidOperationException("Timeout seconds not set");
        var circuitFailures = vendor.CircuitBreakAfterFailures
                              ?? defaults.CircuitBreakAfterFailures
                              ?? throw new InvalidOperationException("Circuit break after failures not set");

        var builder = new ResiliencePipelineBuilder();

        if (maxRetries > 0 && delaySec > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromSeconds(delaySec),
                BackoffType = DelayBackoffType.Constant,
                OnRetry = OnRetryLogger,
                ShouldHandle = new PredicateBuilder()
                    .Handle<TimeoutRejectedException>()
                    .Handle<HttpRequestException>()
                    .HandleResult(RetryPredicateHandler),
            });
        }

        if (timeoutSec > 0)
        {
            builder.AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutSec),
                OnTimeout = OnTimeoutLogger
            });
        }

        if (circuitFailures > 0)
        {
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = circuitFailures,
                BreakDuration = TimeSpan.FromSeconds(15),
                OnOpened = args =>
                {
                    var logs = GetOrCreateLogTrace(args.Context);
                    logs.Add(
                        $"[{LogType.CircuitBreaker}][OPEN] Failure criteria met! Outbound HTTP requests blocked for {args.BreakDuration.TotalSeconds}s.");

                    return default;
                },
                OnClosed = args =>
                {
                    var logs = GetOrCreateLogTrace(args.Context);
                    logs.Add(
                        $"[{LogType.CircuitBreaker}][CLOSED] Integration health restored. Allowing standard traffic.");

                    return default;
                },
                OnHalfOpened = args =>
                {
                    var logs = GetOrCreateLogTrace(args.Context);
                    logs.Add(
                        $"[{LogType.CircuitBreaker}][HALF] Testing integration health with a limited probe stream.");

                    return default;
                }
            });
        }

        builder.AddChaosLatency(new ChaosLatencyStrategyOptions
        {
            Enabled = chaos.LatencyEnabled,
            InjectionRate = chaos.LatencyInjectionRate,
            Latency = TimeSpan.FromSeconds(chaos.LatencySeconds),
            OnLatencyInjected = args =>
            {
                GetOrCreateLogTrace(args.Context)
                    .Add($"[{LogType.Chaos}] Injected {args.Latency.TotalSeconds}s latency");
                return default;
            }
        });

        builder.AddChaosFault(new ChaosFaultStrategyOptions
        {
            Enabled = chaos.FaultEnabled,
            InjectionRate = chaos.FaultInjectionRate,
            FaultGenerator = _ => ValueTask.FromResult<Exception?>(
                new HttpRequestException("Simmy injected fault", inner: null,
                    statusCode: HttpStatusCode.InternalServerError)),
            OnFaultInjected = args =>
            {
                GetOrCreateLogTrace(args.Context)
                    .Add($"[{LogType.Chaos}] Injected fault: {args.Fault.GetType().Name}");
                return default;
            }
        });

        return builder.Build();
    }

    private static List<string> GetOrCreateLogTrace(ResilienceContext context)
    {
        if (context.Properties.TryGetValue(LogTraceKey, out var logs))
        {
            return logs;
        }

        var newLogList = new List<string>();
        context.Properties.Set(LogTraceKey, newLogList);

        return newLogList;
    }
    
    
    private static ValueTask OnRetryLogger(OnRetryArguments<object> args)
    {
        if (!args.Context.Properties.TryGetValue(LogTraceKey, out var logs))
        {
            return default;
        }

        var reason = string.Empty;

        if (args.Outcome.Result is HttpResponseMessage message)
        {
            reason = message.StatusCode.ToString();
        }

        if (args.Outcome.Exception is not null)
        {
            reason = args.Outcome.Exception.GetType().Name;
        }

        logs.Add($"[{LogType.Retry}] Attempt #{args.AttemptNumber + 1} due to: {reason}. Waiting {args.RetryDelay.TotalSeconds}s...");
        return default;
    }

    private static bool RetryPredicateHandler(object res)
    {
        if (res is HttpResponseMessage response)
        {
            return response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout;
        }

        return false;
    }
    
    private static ValueTask OnTimeoutLogger(OnTimeoutArguments args)
    {
        var logs = GetOrCreateLogTrace(args.Context);
        logs.Add($"[{LogType.Timeout}] Timeout exceeded after {args.Timeout.TotalSeconds}s limit");

        return default;
    }
}