# Boutquin.Mediator.Caching

`CachingBehavior<TRequest, TResponse>` — a `Boutquin.Mediator` pipeline behavior that stores request responses in an `IDistributedCache` and serves repeat requests from it.

## Opting in

Only request types marked `[CachingPolicy]` are cached; every other request passes through untouched.

```csharp
[CachingPolicy]
public sealed record GetExchangeRates(string BaseCurrency) : IQuery<Result<RatesDto>>;
```

Per-request settings bind from configuration at `MediatorSettings:{request type full name}:CacheSettings`:

```json
{
  "MediatorSettings": {
    "MyApp.Rates.GetExchangeRates": {
      "CacheSettings": {
        "AbsoluteExpirationRelativeToNow": "00:10:00",
        "SlidingExpiration": "00:01:00",
        "PerUser": true
      }
    }
  }
}
```

| Setting | Default | Effect |
|---------|---------|--------|
| `Disabled` | `false` | Skip caching for this request type. |
| `Reset` | `false` | Remove the entry and run the handler, then store the fresh response. |
| `AbsoluteExpirationRelativeToNow` | 5 minutes | Absolute lifetime of an entry. |
| `SlidingExpiration` | 30 seconds | Lifetime extended by each read. |
| `PerUser` | `false` | Prefix the key with `ICurrentUserService.UserId`. An anonymous caller bypasses the cache entirely rather than sharing an entry. |

## Behavior notes

- The cache key is built from the request type and its public property values (`RequestExtensions.GetCacheKey`).
- Only a success `Result` is stored. A failure is returned to the caller but not cached, so the next call runs the handler again and a transient error does not outlive its cause.
- A corrupt or type-incompatible cache entry is logged and ignored, and the handler runs instead.

## Registration

```csharp
services.AddDistributedMemoryCache(); // or any IDistributedCache
services.AddMediator(
    typeof(GetExchangeRatesHandler).Assembly,
    typeof(CachingBehavior<,>).Assembly);
```

`CachingBehavior` also needs `ICurrentUserService` (from `Boutquin.Abstractions`) and `IConfiguration` in the container.

Source, architecture notes and changelog: https://github.com/boutquin/Boutquin.Mediator
