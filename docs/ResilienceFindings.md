# Spike findings: resilience, retries, backoff and circuit breakers
> Chris Kelly

This spike explored how we can handle downstream failures more gracefully, so that each dependency can fail independently and show its own "service unavailable" behaviour rather than taking the whole experience down with it. The goal was to build a practical starting point for understanding how Polly-based resilience patterns could be applied in this repository.

## What was built

- A small emulated integration host with x independent downstream services. Initially with three, each able to simulate slow responses and failures.
- A shared resilience layer built around Polly to apply retries, timeouts and circuit breakers per service.
- A lightweight dashboard that queries each service independently and reports status per dependency, rather than treating every failure as a single shared outage [this was only implemented to make visuaulation of what Polly was doing easier to view compared to consuming logs & json responses].

## Why this matters

- Retries help absorb transient faults such as brief timeouts or temporary network issues.
- Backoff reduces the chance of hammering a downstream service while it is recovering.
- Timeouts prevent calls from hanging indefinitely when a dependency is slow or unresponsive.
- Circuit breakers stop a repeatedly failing dependency from being hit continuously, giving it a chance to recover.

## What the playground demonstrates

The core library centralises resilience pipeline creation so each integration can be configured with its own policy settings. The dashboard and monitoring flow both use the same pattern, which makes the behaviour easier to reason about and extend. The emulated services provide a controlled environment to test how retries, timeouts and circuit breakers behave under predictable failure conditions.

## Key benefits

- Failures stay isolated to the affected dependency.
- The system degrades more gracefully and can still return partial information.
- We gain a practical baseline for future work around observability, policy tuning and fallback behaviour.

## Comparing to resilience defaults

When you use AddStandardResilienceHandler() in .NET 8+ (via Microsoft.Extensions.Resilience), you are getting a production-ready, opinionated pipeline rather than a blank slate. It stacks several resilience strategies together so common transient faults are handled automatically without needing to wire everything up manually.

The default pipeline is designed to be safe and broadly useful for many services, but it is still a general-purpose baseline rather than a perfect fit for every dependency. In practice, it usually includes a combination of:

- rate limiting to prevent a dependency from being overwhelmed
- an overall timeout so the full request does not hang indefinitely
- retries with backoff to absorb short-lived failures
- a circuit breaker to stop repeated failures from hammering a struggling service
- an attempt timeout so each individual call attempt is bounded

A typical default set looks like this:

| Order | Strategy | Description | Typical defaults |
| --- | --- | --- | --- |
| 1 | Rate limiter | Limits the number of concurrent requests being sent to a dependency. | Queue: `0`, Permit: `1,000` |
| 2 | Total timeout | Applies an overall timeout to the full execution, including retries. | Total timeout: `30s` |
| 3 | Retry | Retries transient failures using backoff. | Max retries: `3`, Backoff: `Exponential`, Use jitter: `true`, Delay: `2s` |
| 4 | Circuit breaker | Opens when too many failures or timeouts occur. | Failure ratio: `10%`, Min throughput: `100`, Sampling duration: `30s`, Break duration: `5s` |
| 5 | Attempt timeout | Limits the time for each individual attempt. | Attempt timeout: `10s` |

The value of this spike is not that these defaults are wrong, but that they are generic. They are a solid starting point, yet they may be too aggressive, too conservative, or simply not well matched to the behaviour of a specific downstream service. By building a custom pipeline we can make the resilience behaviour more service-aware.

That is the real benefit of this approach. Instead of applying one fixed policy to everything, we can tune retries, timeouts and circuit-breaker thresholds differently depending on a service’s known behaviour, expected latency, or operational context. For example, a service that is known to have occasional peak loads might need a higher timeout or a different circuit-breaker threshold, while another service might need fewer retries to avoid amplifying load during an outage.

In short, the platform defaults give us a sensible baseline, but a custom pipeline gives us the flexibility to make resilience more deliberate and more aligned to the downstream dependencies we are actually depending on.

## My suggested next steps

1. Tune/Understand retry and timeout values per service rather than relying on a single shared policy, this could be a multiple mixed state unit integration test which emulates failures with `Simmy` and outputs findings.
2. Look at fallback behaviour or cached responses for more resilient user journeys.
3. Look deeper at adding telemetry and tracing so retry and circuit-breaker transitions are visible in operation - this could accidently overlap with Spike B.
4. Look into how resilience behaves in a multi-instance deployment and whether Redis or a shared coordination mechanism would be needed to avoid duplicate health checks or conflicting circuit-breaker states.