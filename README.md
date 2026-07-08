# SchoolAccount.ResiliencePlayground

A small playground for building and running resilience/emulation test integrations used by the SchoolAccount project. This repo contains a lightweight Dashboard and several Emulated Integration endpoints you can run locally to simulate slow or failing downstream services.

## Projects

| Project | Description |
| --- | --- |
| `SchoolAccount.ResiliencePlayground` | core library and shared models. |
| `SchoolAccount.ResiliencePlayground.Dashboard` | a small ASP.NET app that shows the health/status of registered services. |
| `SchoolAccount.ResiliencePlayground.EmulatedIntegration` | multiple small endpoints that emulate downstream integrations (A, B, C). |

## Build
From the repository root run:

```bash
dotnet build
```

or build a specific project:

```bash
dotnet build SchoolAccount.ResiliencePlayground.Dashboard
```

## Run individual services

- Dashboard (HTTP profile)

```bash
dotnet run --project SchoolAccount.ResiliencePlayground.Dashboard --launch-profile http
```

- Emulated Integration (pick a profile A/B/C)

```bash
dotnet run --project SchoolAccount.ResiliencePlayground.EmulatedIntegration --launch-profile http-A
dotnet run --project SchoolAccount.ResiliencePlayground.EmulatedIntegration --launch-profile http-B
dotnet run --project SchoolAccount.ResiliencePlayground.EmulatedIntegration --launch-profile http-C
```

Each `http-*` launch profile sets up a different `Environment` configuration. See `SchoolAccount.ResiliencePlayground.EmulatedIntegration/Properties/launchSettings.json` for the environment variables used by each profile (for example `Environment__ErrorRate`, `Environment__SlowRate`, `Environment__SlowDelayMs`, and `Environment__Name`).

## Run "All Emulated" (recommended for local testing)

If your using Rider you can consume the `.run/All Emulated.run.xml` as it has a Compound run mode which allows you to just spin multiple applications at once, where it will run each instance if it was a seperate terminal, and then can run the Dashboard project seperately.

Note: Running backgrounded processes this way will print logs to the same terminal. For a cleaner dev experience use separate terminals or a process manager.

## Default Local Endpoints
- Emulation A: http://localhost:5001
- Emulation B: http://localhost:5002
- Emulation C: http://localhost:5003
- Dashboard: http://localhost:5124

Useful endpoints provided by the emulated integration:
- `/about`: returns the `EnvironmentSettings` for that instance.
- `/health`: performs the emulation (may return 503 when the emulation fails).
- `/tasks?amount=<n>`: returns a small collection of task entities (useful sample data).

The Dashboard exposes `/` for a JSON payload and `/status` for a simple HTML dashboard.

## Configuration
- Per-instance settings are read from `appsettings.json`, and environment variables. The emulated integration maps settings under the `Environment` section into `EnvironmentSettings` (see `Environment__*` environment variable keys used in `launchSettings.json`).

Example to run Emulation B with a different error rate inline:

```bash
Environment__ErrorRate=0.2 dotnet run --project SchoolAccount.ResiliencePlayground.EmulatedIntegration --launch-profile http-B
```

## Service Registry & Startup Wiring

Two pieces connect configuration to the resilience factory: `AddServiceRegistry` registers everything at startup, and `ServiceRegistry` holds the result for use at runtime.

### `AddServiceRegistry(builder)`

A `WebApplicationBuilder` extension that reads the `IntegrationSettings` config section and wires up one HTTP client and one resilience pipeline per vendor. Call it once during startup:

```csharp
builder.AddServiceRegistry();
```

What it does, in order:

1. Reads the `IntegrationSettings` section (name from `IntegrationSettings.SectionName`) and throws `InvalidOperationException` if it's missing.
2. Binds the section with `Configure<IntegrationSettings>` so `IOptions<IntegrationSettings>` is injectable elsewhere.
3. Also materialises a concrete `IntegrationSettings` instance for use during startup, throwing if binding returns `null`.
4. For each vendor in `Services`:
   - Registers a **named** `HttpClient` (named after `ServiceName`) with its `BaseAddress` set to the vendor's `BaseUrl`.
   - Builds the vendor's pipeline via `ResiliencePipelineFactory.Create(vendor, Defaults)` and registers it in the `ServiceRegistry`.
5. Registers the populated `ServiceRegistry` as a singleton.

The config section it expects:

```
IntegrationSettings
├─ Services   → ServiceManifest[]   (per-vendor name, base URL, optional resilience overrides)
└─ Defaults   → ResilienceSettings  (fallback retry / timeout / circuit values)
```

> **Design note:** the resilience pipeline is *not* attached to the named `HttpClient` (e.g. via `AddResilienceHandler`). The client and its pipeline are stored side by side, so a caller resolves the named client from `IHttpClientFactory`, looks its pipeline up in the registry, and runs the request through `pipeline.ExecuteAsync(...)` itself (see [Request log trace](#request-log-trace) for that call pattern).

### `ServiceRegistry`

A thread-safe store of registered services, backed by a `ConcurrentDictionary` keyed on the **lower-cased** service name, so every lookup is case-insensitive.

| Member | Behaviour |
|---|---|
| `Register(manifest, pipeline)` | Adds or replaces a `Service` built from the manifest's name and base URL plus its resilience pipeline. |
| `Get(serviceName)` | Case-insensitive lookup; returns `null` when the service isn't registered. |
| `All()` | Enumerates every registered `Service`. |
| `UpdateStatus(serviceName, status)` | Appends a `ServiceState` to that service's `History`. |

**Notes**
- `UpdateStatus` uses the dictionary indexer, so it throws `KeyNotFoundException` for an unregistered service, unlike `Get`, which returns `null`. Guard the name (or call `Get` first) if it might not exist.
- The dictionary is concurrent, but each `Service.History` is a plain `List<ServiceState>`. Concurrent `UpdateStatus` calls for the same service aren't synchronised, so add locking there if statuses can be written from multiple threads.

## Resilience Pipeline Factory

`ResiliencePipelineFactory` is a static factory that builds [Polly v8](https://www.pollydocs.org/) resilience pipelines for outbound HTTP calls. It centralises retry, timeout, and circuit-breaker policy so every vendor integration behaves consistently, and it threads a per-request log trace through each policy so you can see exactly which resilience events fired during a call.
Just one thing to note, it is apart of the .Net Extensions now as [Microsoft.Extensions.Resilience](https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.Resilience), but still referenced as Polly.

It exposes two pipelines:

| Method | Returns | Purpose |
|---|---|---|
| `Monitor()` | `ResiliencePipeline` | Lightweight retry-only pipeline for internal/monitoring calls. |
| `Create(vendor, defaults)` | `ResiliencePipeline<HttpResponseMessage>` | Full retry + timeout + circuit-breaker pipeline for a specific vendor integration. |

### `Create(vendor, defaults)`

Builds the main pipeline for a vendor. It layers three strategies (order matters, see below) and resolves every tuning value from configuration.

#### Configuration resolution

Each value is taken from the vendor manifest first, then the global defaults, and throws if neither supplies it:

```
value = vendor.X ?? defaults.X ?? throw InvalidOperationException
```

| Value | Source property | Used by |
|---|---|---|
| Max retry attempts | `MaxRetryAttempts` | Retry |
| Delay (seconds) | `DelaySeconds` | Retry |
| Timeout (seconds) | `TimeoutSeconds` | Timeout |
| Circuit break threshold | `CircuitBreakAfterFailures` | Circuit breaker (`MinimumThroughput`) |

A vendor can override any single value while inheriting the rest from defaults, and a missing value fails fast at build time rather than mid-call.

#### Strategy layers

Polly executes strategies in the order they're added, with the **first added being the outermost**. A call flows through:

```
Retry  (outermost re-runs the whole inner stack on failure)
 └─ Timeout  (fresh timeout applied to each attempt)
     └─ Circuit Breaker  (tracks the health of individual attempts)
         └─ actual HTTP call
```

Because the timeout sits *inside* the retry, each attempt gets its own fresh timeout window, and a timed-out attempt surfaces as a `TimeoutRejectedException` that the retry strategy then catches and retries.

**Retry**
- Runs up to *max retry attempts* times with a constant delay of *delay seconds*.
- Retries on `TimeoutRejectedException`, `HttpRequestException`, or an HTTP result of `503 Service Unavailable` / `408 Request Timeout`.
- Logs each retry with the triggering reason.

**Timeout**
- Cancels an attempt that exceeds *timeout seconds*.
- Logs when the limit is hit.

**Circuit breaker**
- Samples calls over a 10-second window (`SamplingDuration`).
- Opens when the failure ratio reaches 50% (`FailureRatio = 0.5`), provided at least *circuit break threshold* calls occurred in that window (`MinimumThroughput`).
- Stays open for 15 seconds (`BreakDuration`), then moves to half-open to probe recovery.
- Logs every state transition (open / closed / half-open).

### `Monitor()`

A minimal, non-generic retry-only pipeline (it doesn't inspect the HTTP response):
- Handles `HttpRequestException` and `TaskCanceledException`.
- 2 attempts, constant 500 ms delay.
- No log-trace hook.

> **Design note:** `Create()` is what the `ServiceMonitor` background service uses. During development, before the Dashboard project's `Query` endpoint existed, `ServiceMonitor` was pointed at `ResiliencePipelineFactory.Monitor()` initially, this change to iron out any *quirks* and understandings of Polly.

### Request log trace

Every strategy writes human-readable events into a shared list carried on the `ResilienceContext`, so callers can inspect what happened after a call completes.

- `LogTraceKey`, a strongly-typed `ResiliencePropertyKey<List<string>>` used to store the trace on the context.
- `GetOrCreateLogTrace(context)`, returns the existing trace list or creates and attaches a new one.
- Each callback (`OnRetry`, `OnTimeout`, `OnOpened`, `OnClosed`, `OnHalfOpened`) appends an entry tagged with a `LogType` (`Retry`, `Timeout`, `CircuitBreaker`).

To read the trace, seed a list on the context before executing and inspect it afterwards:

```csharp
var context = ResilienceContextPool.Shared.Get();
var trace = ResiliencePipelineFactory.GetOrCreateLogTrace(context);

var response = await pipeline.ExecuteAsync(
    async ctx => await httpClient.SendAsync(request, ctx.CancellationToken),
    context);

// trace now holds the resilience events that fired during the call
ResilienceContextPool.Shared.Return(context);
```

## Service Monitoring (background service)

`ServiceMonitoring` is a hosted `BackgroundService` that continuously health-checks every configured integration and records the result in the `ServiceRegistry`. Each probe runs through that service's own resilience pipeline, so retries, timeouts, and circuit-breaking apply to the health checks exactly as they would to real traffic, and each probe's resilience events are captured in the per-request log trace.

Injected dependencies:

| Dependency | Used for |
|---|---|
| `IHttpClientFactory` | Resolves the named `HttpClient` registered for each service. |
| `ServiceRegistry` | Looks up each service's pipeline and writes health results back. |
| `IOptions<IntegrationSettings>` | Supplies the service list and default thresholds. |
| `ILogger<ServiceMonitoring>` | Startup and failure logging. |

### `ExecuteAsync`

The `BackgroundService` entry point fans out one independent monitoring loop per configured service, runs them all concurrently, and awaits them together with `Task.WhenAll`. A slow or blocked check on one service therefore doesn't hold up the others.

### `MonitorService` (per-service loop)

Each loop polls a single service until cancellation. One pass does the following:

1. Looks the service up with `registry.Get(...)`. If it isn't registered yet, waits 2 seconds and retries.
2. Resolves the named `HttpClient` and starts a `Stopwatch`.
3. Seeds a fresh log list (opening with a `[Start]` entry) and attaches it to a pooled `ResilienceContext` via `ResiliencePipelineFactory.LogTraceKey`, so the pipeline's own callbacks (retry / timeout / circuit-breaker) append into the same list.
4. Executes `GET {HealthEndpoint}` through the service's pipeline, passing the `HttpClient` as `state` to avoid a per-call closure allocation.
5. Classifies the outcome (see below) and appends a `[Complete]` or `[Failed]` entry.
6. Persists a new `ServiceState`, status + `ServicePerformance` (status code and elapsed ms) + the collected logs, via `registry.UpdateStatus(...)`.
7. Returns the `ResilienceContext` to the pool, then waits 10 seconds before the next probe.

### Status classification

The outcome is mapped to a `ServiceStatus`:

| Result | Status |
|---|---|
| Success response within the degraded threshold | `Healthy` |
| Success response slower than the degraded threshold | `Degraded` |
| Non-success status code | `Error` |
| Exception after the pipeline exhausts its retries | `Error` (with the exception message) |

The latency threshold is resolved the same way as the pipeline settings, the vendor value first, then the default: `service.DegradedResponseThresholdSeconds ?? Defaults.DegradedResponseThresholdSeconds`.

### Timing

- **10 s** between successive probes of a healthy/registered service.
- **2 s** back-off when the service isn't in the registry yet.

### Side notes

- The 10 s inter-probe delay swallows `TaskCanceledException` on shutdown, but the 2 s "not registered yet" delay does not, so a shutdown that lands during that wait can surface a `TaskCanceledException` out of the loop.
- Each service is monitored by exactly one loop, so writes to that service's `History` are single-writer from the monitor's side. A Dashboard endpoint reading `History` while the monitor writes is still an unsynchronised read (see the `ServiceRegistry` notes).
- The `catch (Exception)` is deliberately broad: by the time the pipeline throws, it has already exhausted its retries, so this records the final failure rather than swallowing a transient one. When the exception is an `HttpRequestException` carrying a status code, that code is captured.
- `var serviceState = registry.Get(...)` actually holds a `Service` (used only for its `.Pipeline`), not a `ServiceState`, the name reads a little confusingly next to the `ServiceState` written back on the following lines.
