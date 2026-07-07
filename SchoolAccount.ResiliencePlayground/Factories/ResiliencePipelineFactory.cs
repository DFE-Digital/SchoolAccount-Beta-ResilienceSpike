using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using SchoolAccount.ResiliencePlayground.Models;

namespace SchoolAccount.ResiliencePlayground.Factories;

public static class ResiliencePipelineFactory
{
    public static readonly ResiliencePropertyKey<List<string>> LogTraceKey = new("RequestLogTrace");

    public static List<string> GetOrCreateLogTrace(ResilienceContext context)
    {
        if (context.Properties.TryGetValue(LogTraceKey, out var logs))
        {
            return logs;
        }

        var newLogList = new List<string>();
        context.Properties.Set(LogTraceKey, newLogList);

        return newLogList;
    }

    public static ResiliencePipeline Monitor()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>().Handle<TaskCanceledException>(),
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Constant
            })
            .Build();
    }

    public static ResiliencePipeline<HttpResponseMessage> Create(ServiceManifest vendor, ResilienceSettings defaults)
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

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = maxRetries,
                Delay = TimeSpan.FromSeconds(delaySec),
                BackoffType = DelayBackoffType.Constant,
                OnRetry = args => {
                    if (args.Context.Properties.TryGetValue(LogTraceKey, out var logs)) {
                        var reason = args.Outcome.Exception?.GetType().Name 
                                     ?? args.Outcome.Result?.StatusCode.ToString();
                        
                        logs.Add($"[{LogType.Retry}] Attempt #{args.AttemptNumber + 1} due to: {reason}. Waiting 1s...");
                    }
                    return default;
                },
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<TimeoutRejectedException>()
                    .Handle<HttpRequestException>()
                    .HandleResult(res => res.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout),
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutSec),
                OnTimeout = (args) =>
                {
                    var logs = GetOrCreateLogTrace(args.Context);
                    logs.Add($"[{LogType.Timeout}] Timeout exceeded after {args.Timeout.TotalSeconds}s limit");

                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
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
            })
            .Build();
    }
}