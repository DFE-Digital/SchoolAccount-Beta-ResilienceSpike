# Spike findings: implementing chaos strategies into a resilience pipeline
> Andy Cunningham

_These findings relate to the work completed in the `SchoolAccount.ResiliencePlayground`, `SchoolAccount.ResiliencePlayground.HealthChecksUI` & `SchoolAccount.ResiliencePlayground.SimmySimplified` projects._

This project builds upon the resilience work explored in ResilienceFindings.md by introducing Simmy into the resiliency pipeline. This is to deliberately inject failure into the resilience pipelines so that retries, timeouts and circuit breakers are proven to behave correctly.

This has been done by injecting chaos, randomness, into the pipelines in a controlled and repeatable approach.

## What was built

- A simple application that allows chaos strategies to be added into a resilience pipeline.
- Expanding upon the exisiting resilience pipeline to implement chaos strategies for each service.
- A simple dashboard using HealthChecks UI that groups both the query and health endpoints for each service so that they could fail independently of each other. [This was purely for better visualisation of the status and error reporting logs].

## Why this matters

- Resilience policies are only trustworthy if they have been exercised. Chaos injection allows you to observe and prove the policies implemented are working as expected.
- Simmy runs in-process inside the resilience pipelines ensuring failures can be injected without standing up emulated endpoints, proxies or touching infrastructure.
- Chaos is expressed as pipeline configuration allowing for replayable and deterministic tests.
- Compliments the emulated integration approach. Emulated endpoints exercise the full HTTP round trip while Simmy can target a specific strategy in isolation with precise control rate and the type of failure.

## What the playground demonstrates

The application shows how chaos can strategies can be layered directly into an existing Polly resilience pipeline. Each strategy can be configured independently for injection rate and failure type. As chaos sits within the pipeline, the retries, timeouts, and circuit breakers are the components reacting to the injected failures which makes their behaviour directly observable and verifiable. Simmy provides a controlled, in-process way to produce faults, latency and fake outcomes on demand without needing emulated endpoints or real downstream instability.

## Chaos strategies Simmy provides

Simmy, which comes natively within Polly V8+, offers four default strategy types.

| Strategy       | Description                                                    | 
|----------------|----------------------------------------------------------------|
| Fault          | Injects exceptions in your system.                             |
| Outcome        | Injects fake outcomes (results or execptions) into your system. |
| Latency        | Injections latency into executions before the calls are made.  |
| Behavior       | Allows you to inject any extra behavior before a call is made. |

Each strategy shares a common set of options; an `InjectionRate` (probability from 0 to 1 inclusive), an `Enabled` flag as well as generator overloads that allow both to be resolved dynamically at runtime. The generator overloads can reduce the magnitude of chaos injection in higher environments for example.

Unless explicitly overriden, chaos strategies once implemented to a pipeline are automatically active with a probability of 0.001 (1 in every 1000 calls will fail). Simmy is designed for you to configure specific delegates at runtime depending upon the context of the call and therefore have no default values for each strategy.

| Property               | Description                                                                                                                                                                                                     | Example values         |
|------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------|
| `InjectionRate`         | The probability defined as a decimal between 0 and 1 inclusive. The strategy will inject the chaos randomly that proportion of the time. This property will be ignored if `InjectionRateGenerator` is specified. | 0.001                  |
| `InjectionRateGenerator` | Generates the injection rate for a given execution.                                                                                                                                                             | null                   |
| `Enabled`               | Determines whether the strategy is enabled or not. This property will be ignored if `EnabledGenerator` is specified.                                                                                            | true                   |
| `EnabledGenerator`      | The generator that indicates whether the chaos strategy is enabled for a given execution.                                                                                                                       | null                   |
| `FaultGenerator`        | Required for Fault strategies. Injects exceptions by utilising runtime information.                                                                                                                             | `HttpRequestException` |
| `OutcomeGenerator`      | Required for Outcome strategies. Injects custom outcomes by utilising runtime information.                                                                                                                      | `HttpStatusCode`       |
| `Latency`               | Defines a fixed delay to be injected.                                                                                                                                                                           | 30 seconds             |
| `LatencyGenerator`      | Dynamically inject delay by utilising runtime informatiion.                                                                                                                                                     | null                   |
| `BehaviorGenerator`     | Required for Behavior strategies. Injects custom behaviour by utilising runtime information. This could be to clear down cache, restart or killing a connection etc.                                            | null                   |

For lower environments a higher `InjectionRate` is recommmended for fast deterministic feedback, between 0.5 - 1 for dev. Low rates will behave more like reality but require more runs to be observed.

Custom chaos strategies can be defined by utilising the `Behavior` strategy. This proactive strategy is designed to inject a custom behavior into the system before and operation is invoked. This allows users to alter inputs, simulate resource exhaustion or put the system in a given state before the operation. The `Behavior` strategy has limitations however as it can only put the system into a certain state before or around the call.

## Telemetry

If the `Behavior` strategy is not sufficient then custom strategies can be implemented but requires additional configuration. Ensure that the custom strategy and the custom strategies options derive from `ChaosStrategy` and `ChaosStrategyOptions` classes so that it can be invoked correctly within the pipeline.

Chaos injections by default are silent and are indistuingishable from genuine failure logs. For better obersvability additional properties for each strategy settings must be configured. These additional properties, `OnFaultInjected`, `OnOutcomeInjected`, `OnLatencyInjected` and `OnBehaviourInjected`, are invoked after the strategy injection has occured and will appear as warning logs within the application.

````
Resilience event occurred. EventName: 'Chaos.OnLatency', Source: '(null)/(null)/Chaos.Latency', Operation Key: '', Result: ''

Resilience event occurred. EventName: 'Chaos.OnLatency', Source: 'MyPipeline/MyPipelineInstance/MyLatencyStrategy', Operation Key: 'MyLatencyInjectedOperation', Result: ''
````

To increase the surfacing of the fault injections pair Simmy with a more robust logging and monitoring tool to detect when a fault injection policy causes a service to degrade. Within this spike HealthChecks UI was implemented to show multiple groups of endpoints and services to quickly identify when a service degraded.

Therefore chaos injection is a cheap way to generate the resilience strategies within the pipeline and verify that the telemetry captures this correctly but requires additional tools to sruface this in a readable format.

## Limitations and risks

As Simmy is implemented directly within the application careful consideration of the chaos strategies and the logic surrounding them must be considered to limit the blast radius of unintended strategy injection. Accidentally leaving high intensity chaos strategies within a production environment can cause widespread outages. However testing solely within the lower environments, dev, test, staging etc, can cause environmental skew.

Failures could cascade with multiple high intensity strategies being invoked at once. Injecting severe latency or faults could inadvertently overwhelm upstream systems that lack sufficient timeout or rate limiting protections. This could be compounded if the chaos strategies have not been configured with the correct settings enabling the detection of when a strategy has been invoked. This could increase the time taken debugging issues.

Simmy is limited to the four base strategies and requires additional configuration for other custom chaos strategies.

## Mitigation

- Isolate tests to specific environments using configuration or feature flags. This is to prevent Simmy running or limit the intensity of the strategies within production environments.
- Introduce faults gradually. Start with internal-only services or a small percentage of traffic.
- Pair Simmy with a more robust logging and monitoring platform/ application for better fault injection detection.

## Further notes

The resilience pipeline spike utilised Polly to emulate downstream endpoints for integration-level behaviour i.e. can the client handle a slow, flaky service. Simmy fills in the picture with strategy-level verification ensuring that the resilience pipeline triggered the correct response. The pairing of the two allows to test, tune and prepare the application for real world turbulence.

## Suggested next steps

1. Build the Simmy-driven test suite with paramaterised tests.
2. Define a standard chaos configuration convention before it is used more widely. This should include the injection rate limits for each environment, enabled by default, naming conventions, etc.
3. Wire in callbacks into the same telemetry explored within Spike B so injected chaos is correctly labelled and more easily distinguished from a genuine incident.


