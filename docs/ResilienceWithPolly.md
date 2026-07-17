# Introduction to resilience with Polly

_These findings are only based off the work completed in the `SchoolAccount.ResiliencePlayground`, `SchoolAccount.ResiliencePlayground.Dashboard` & `SchoolAccount.ResiliencePlayground.EmulatedIntegration` projects. There was knowledge taken from [Resilience and chaos engineering - .NET Blog](https://devblogs.microsoft.com/dotnet/resilience-and-chaos-engineering/) running alongside findings with the _

This spike was a quick introduction to using Polly for downstream resilience. The goal was to show that a service can fail more gracefully when a dependency is slow, throwing, or unavailable, instead of letting one bad dependency bring down the whole experience.

## What was done?

A small playground was built with three emulated downstream services and a simple dashboard. The dashboard calls each service independently and shows whether it is healthy, degraded, or failing state, with all there logs of when a stratgory has triggered. This makes it easy to see how retries, timeouts, and circuit breakers behave without needing a full production setup.

## What problem does Polly solve?

Polly is a .NET resilience library. It helps protect an application from transient faults such as:

- temporary network issues
- short-lived timeouts
- occasional 5xx or 408 responses
- dependency overload or recovery periods

In practice, Polly lets you add resilience policies such as retries, backoff, timeouts, and circuit breakers so the app degrades gracefully rather than hanging or failing hard.

## A simple example: add resilience to an HttpClient

In a new .NET app, the simplest starting point is to attach a resilience handler directly to an HttpClient:

```csharp
services.AddHttpClient("Service", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001");
})
.AddStandardResilienceHandler(options =>
{
    // Configure each strategry

    // options.Retry, options.Timeout.Timeout, options.CircuitBreaker
    // would be your common alterations, example:

    options.Retry.MaxRetries = 30;
    // This would override the default amount of `3` to `30`, see below for more.
});
```

The important idea is that the resilience handler sits in front of the outbound call. If the dependency is slow or failing, Polly can retry, stop repeated calls, or fail fast in a controlled way.

Here are the default strategry properties for each step preconfigured within Polly:

| Order | Strategy | Description | Typical defaults |
| --- | --- | --- | --- |
| 1 | Rate limiter | Limits the number of concurrent requests being sent to a dependency. | Queue: `0`, Permit: `1,000` |
| 2 | Total timeout | Applies an overall timeout to the full execution, including retries. | Total timeout: `30s` |
| 3 | Retry | Retries transient failures using backoff. | Max retries: `3`, Backoff: `Exponential`, Use jitter: `true`, Delay: `2s` |
| 4 | Circuit breaker | Opens when too many failures or timeouts occur. | Failure ratio: `10%`, Min throughput: `100`, Sampling duration: `30s`, Break duration: `5s` |
| 5 | Attempt timeout | Limits the time for each individual attempt. | Attempt timeout: `10s` |

Most likely would all for a service to pass it in it's own overrides if a service requires a longer retry delay for example.

> **Note:** the above example is how to consume Polly's standard handlers whilst the `ResiliencePipelineFactory` within `SchoolAccount.PresiliencePlayground` was built to allow for custom pipelines to be quickly configured and tweaked during R&D.

## What can be overridden in the sample endpoint?

The sample in this repository uses a small configuration model with these override points:

- MaxRetryAttempts: how many retries to attempt
- DelaySeconds: how long to wait before retrying
- TimeoutSeconds: how long each attempt is allowed to run
- CircuitBreakAfterFailures: how many failures trigger the circuit breaker
- DegradedResponseThresholdSeconds: how slow a response is before the service is marked as degraded

Durring investigation of the `/query` endpoint which was to try illustrate consuming data from downstream services, the below settings seemed to be better defaults for our situations as they helped the service fail fast instead of hanging on a given service which is struggling:

- Retry attempts: 3
- Retry delay: 1 second
- Timeout: 2 to 3 seconds
- Circuit breaker threshold: 5 failures
- Degraded threshold: 2 seconds

These values are a sensible starting point, but they should be tuned per dependency. A critical service may need a longer timeout, while a noisy one may need fewer retries to avoid amplifying the problem.

## What was fpund?

The main finding is that resilience is worth adding early, even in a small spike. The biggest benefits are:

- failures stay isolated to one dependency
- the app can still return partial information
- the user experience is less brittle during transient issues
- the service can recover more gracefully when a downstream dependency is struggling

We also found that the default Polly-style behaviour is a good baseline, but it should be tuned for real service characteristics rather than treated as a one-size-fits-all answer.

## How should a service handle timeouts and Polly errors?

Polly will typically surface a failure once retries and circuit-breaker logic have been exhausted. In a real service, that should be handled in one of three ways:

1. Return a controlled failure to the caller, such as 503 or 504
2. Use a fallback response or cached data where appropriate
3. Mark the dependency as degraded and continue serving the rest of the request

In this sample, the query handler records the failure and marks the service as error or degraded. That is a good pattern for a new project because it keeps the failure visible without crashing the whole request flow.

## Recommendation

For a new project, start with the standard resilience handler and tune it per dependency rather than trying to hand-roll everything from scratch. It is the fastest path to getting useful resilience in place, and it is easier to maintain than a bespoke pipeline unless you need very specific behaviour.

## How to rerun the test application

From the repository root, build the solution:

```bash
dotnet build
```

Then run the emulated services and the dashboard:

```bash
dotnet run --project SchoolAccount.ResiliencePlayground.EmulatedIntegration --launch-profile http-A
dotnet run --project SchoolAccount.ResiliencePlayground.EmulatedIntegration --launch-profile http-B
dotnet run --project SchoolAccount.ResiliencePlayground.EmulatedIntegration --launch-profile http-C
dotnet run --project SchoolAccount.ResiliencePlayground.Dashboard --launch-profile http
```

You can then open the dashboard at:

- http://localhost:5124/ => a dirty dashboard style monitor.
- http://localhost:5124/status => a json response of what the dashboard consumes.

If you want to tweak the behaviour, adjust the values in the app settings for the sample integration and rerun the services.