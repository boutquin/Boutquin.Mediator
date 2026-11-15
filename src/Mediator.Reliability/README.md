# Boutquin.Mediator.Reliability

`RetryBehavior<TRequest, TResponse>` — a `Boutquin.Mediator` pipeline behavior that retries a request whose handler throws, with a per-attempt timeout, using `Boutquin.Reliability`.

## Opting in

Retry is **off by default**. Enable it per request type at `MediatorSettings:{request type full name}:RetrySettings`:

```json
{
  "MediatorSettings": {
    "MyApp.Payments.ChargeCard": {
      "RetrySettings": {
        "Enabled": true,
        "NumberOfRetries": 3,
        "TimeoutPerAttempt": "00:00:05",
        "ContinueAfterTimeout": true,
        "SleepDurationsMs": [ 100, 500, 2000 ]
      }
    }
  }
}
```

| Setting | Default | Effect |
|---------|---------|--------|
| `Enabled` | `false` | Turns retry on for this request type. |
| `NumberOfRetries` | 2 | Retries after the first attempt. Negative values become 0. |
| `TimeoutPerAttempt` | 10 seconds | Timeout for each attempt. Non-positive values become 10 seconds. |
| `ContinueAfterTimeout` | `false` | Whether a timed-out attempt is retried. |
| `SleepDurationsMs` | `[100]` | Delay before retry *n*; the last value repeats once the list runs out. |

Only exceptions trigger a retry. A handler that returns a failure `Result` completes the request on the first attempt. When every attempt throws, the last exception propagates. Cancelling the request's token stops the retry loop. Retries re-run the rest of the pipeline, so handlers should be idempotent.

## Registration

```csharp
services.AddSingleton<IWaitWithRetry, WaitWithRetry>();
services.AddMediator(
    typeof(ChargeCardHandler).Assembly,
    typeof(RetryBehavior<,>).Assembly);
```

Source, architecture notes and changelog: https://github.com/boutquin/Boutquin.Mediator
