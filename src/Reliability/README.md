# Boutquin.Reliability

Retry-with-timeout execution built on Polly. `IWaitWithRetry` runs an operation under a per-attempt timeout and retries it when it throws, sleeping between attempts according to an `IWaitSettings` schedule. It has no dependency on the mediator and can be used on its own.

## Example

```csharp
using Boutquin.Reliability;

var settings = new WaitSettings(
    ContinueAfterTimeout: true,
    SleepTimeDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)),
    NumberOfRetries: 3,
    TimeoutPerAttempt: TimeSpan.FromSeconds(5));

IWaitWithRetry retry = new WaitWithRetry(logger);
var quote = await retry.WaitWithRetryAsync(() => client.GetQuoteAsync(symbol), settings, cancellationToken);
```

## Semantics

- Each attempt runs under `TimeoutPerAttempt` (pessimistic timeout: the attempt is abandoned when the timeout elapses).
- An attempt that throws is retried up to `NumberOfRetries` times. A timed-out attempt is retried only when `ContinueAfterTimeout` is `true`.
- `SleepTimeDurationProvider(attempt)` gives the delay before retry `attempt` (1-based).
- When every attempt throws, the task faults with the last exception. A returned value — whatever it is — ends the loop.
- Cancelling the token interrupts both the sleep and the in-flight attempt and surfaces as `OperationCanceledException`.
- `WaitSettings` validates its arguments: a `null` provider is rejected, a negative retry count becomes 0, and a non-positive timeout becomes 10 seconds.

Each method has an overload without a `CancellationToken`.

Source, architecture notes and changelog: https://github.com/boutquin/Boutquin.Mediator
