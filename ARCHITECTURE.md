# Architecture

This document explains how the Boutquin.Mediator packages fit together. For a feature overview, see [README.md](README.md).

## Layers

The solution contains seven independently packaged libraries plus three test projects:

```
src/Abstractions/            Boutquin.Abstractions — current user, authorization service, [Authorize]
src/Reliability/             Boutquin.Reliability — retry-with-timeout on Polly (no mediator dependency)
src/Mediator.Abstractions/   Boutquin.Mediator.Abstractions — request/handler/pipeline contracts
src/Mediator/                Boutquin.Mediator — mediator implementation + DI registration (Scrutor)
src/Mediator.Authorization/  Boutquin.Mediator.Authorization — AuthorizationBehavior
src/Mediator.Caching/        Boutquin.Mediator.Caching — CachingBehavior
src/Mediator.Reliability/    Boutquin.Mediator.Reliability — RetryBehavior
tests/Tests.UnitTests/                 unit tests
tests/Mediator.Tests.IntegrationTests/ end-to-end tests through a real ServiceProvider
tests/Mediator.Tests.Common/           shared request/handler fixtures
```

Dependencies point one way:

```
Boutquin.Domain (Result, Error, INotification)
  ├─ Boutquin.Abstractions
  └─ Boutquin.Mediator.Abstractions
       ├─ Boutquin.Mediator
       ├─ Boutquin.Mediator.Authorization ──── Boutquin.Abstractions
       ├─ Boutquin.Mediator.Caching ────────── Boutquin.Abstractions
       └─ Boutquin.Mediator.Reliability ────── Boutquin.Reliability (Polly)
```

The behavior packages depend on the contracts, never on `Boutquin.Mediator` itself, so a handler assembly can reference the abstractions alone. `Boutquin.Domain` is consumed at an exact version, and each package pins its sibling packages to the same exact version, so the family always restores as one coherent set.

## The Result contract

```
IRequest<TResult>  where TResult : Result
  ├─ ICommand<TResult>
  │    └─ ICommand              (TResult = Result)
  └─ IQuery<TResult>

IStreamRequest<T>               handler yields IAsyncEnumerable<Result<T>>
INotification                   (Boutquin.Domain) — handled by every INotificationHandler<T>
```

**Why constrain responses to `Result`?** A pipeline behavior has to be able to stop a request without knowing what the handler would have returned. Because every response is a `Result`, `AuthorizationBehavior` can return `Error.Forbidden`, `CachingBehavior` can return a deserialized response, and a pre-processor can return any failure — each as the request's typed `Result<T>`. The behaviors construct the generic failure with cached reflection, once per value type.

## Dispatch pipeline

```
SendAsync(request)
  │
  ├─ resolve IRequestHandler<TRequest,TResponse>      missing → InvalidOperationException
  ├─ IRequestPreProcessor<TRequest>  (in order)       failure → return it
  ├─ IPipelineBehavior<TRequest,TResponse>            first registered = outermost
  │     └─ … └─ handler.HandleAsync
  └─ IRequestPostProcessor<TRequest,TResponse>        first failure replaces the response
```

`PublishAsync` resolves every `INotificationHandler<T>`, runs them all even after a failure, and returns the first failure (or success when there are none). `CreateStream` resolves one stream handler and wraps it in `IStreamPipelineBehavior` instances the same way.

Cancellation is not a `Result`: an `OperationCanceledException` from any stage propagates to the caller unchanged.

## Registration and lifetimes

`AddMediator(assemblies)` registers `IMediator` as **scoped**, with `ISender`, `IPublisher` and `IStreamer` forwarding to the same per-scope instance, then uses Scrutor to register every handler, behavior and processor it finds in the given assemblies as **transient**.

**Why scoped, not singleton?** A singleton mediator would capture the root `IServiceProvider`. Scoped handler dependencies (a `DbContext`, a per-request user service) would then fail `ValidateScopes`, and transient disposable handlers would live until the root provider is disposed. Scoping the mediator makes it resolve from the request's scope.

Behaviors live in their own assemblies, so the composition root opts in by passing those assemblies to `AddMediator`.

## Configuration-driven behaviors

Both configurable behaviors read a per-request section keyed by the request type's full name:

```
MediatorSettings:{RequestTypeFullName}:CacheSettings   → CachingPolicySettings
MediatorSettings:{RequestTypeFullName}:RetrySettings   → RetrySettings
```

| Behavior | Opt-in | Default when unconfigured |
|----------|--------|---------------------------|
| `AuthorizationBehavior` | `[Authorize]` on the request type | pass-through |
| `CachingBehavior` | `[CachingPolicy]` on the request type | cache for 5 min absolute / 30 s sliding, shared key |
| `RetryBehavior` | `Enabled: true` in configuration | pass-through (retry off) |

**Why are the settings mutable classes?** `IConfiguration.Bind` populates settable properties. Out-of-range values are coerced in the setters — a negative retry count becomes 0 and a non-positive timeout becomes 10 seconds — so a bad configuration value cannot reach Polly, which rejects them at policy construction.

**Per-user caching fails closed.** With `PerUser: true`, the cache key is prefixed with the caller's user id. An anonymous caller bypasses the cache rather than reading or writing a shared entry, so one user can never be served another's response.

**Only successes are cached.** `CachingBehavior` returns a failure `Result` without storing it. A failure is often transient, and caching it would keep serving the error for the entry's lifetime after its cause had cleared.

**Retry is exception-driven.** `RetryBehavior` delegates to `IWaitWithRetry`, which retries attempts that throw. A failure `Result` is a decision the handler made, so it is returned on the first attempt.

## Component navigation

| Concern | Start here |
|---------|-----------|
| Request and handler contracts | `src/Mediator.Abstractions/IRequest.cs`, `IRequestHandler{TRequest,TResponse}.cs` |
| Pipeline execution | `src/Mediator/Helpers/Mediator.cs` |
| DI registration | `src/Mediator/Extensions/ServiceCollectionExtensions.cs` |
| Authorization | `src/Mediator.Authorization/AuthorizationBehavior.cs` |
| Caching and cache keys | `src/Mediator.Caching/CachingBehavior.cs`, `src/Mediator.Abstractions/RequestExtensions.cs` |
| Retry | `src/Mediator.Reliability/RetryBehavior.cs`, `src/Reliability/WaitWithRetry.cs` |
