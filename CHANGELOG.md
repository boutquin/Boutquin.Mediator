# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - prepared 2026-11-14 (not yet published)

> Release notes prepared on 2026-11-14. Version 1.0.0 has not yet been tagged or published.
> This is the first public release of all seven packages. It establishes the stable public API
> baseline; earlier `0.0.0-beta` builds were never published to nuget.org.

### Added

- **`Boutquin.Mediator.Abstractions`** — request, command (`ICommand`, `ICommand<T>`), query (`IQuery<T>`),
  notification, and stream-request contracts with their handlers; `IPipelineBehavior<TRequest, TResponse>`,
  `IStreamPipelineBehavior<TRequest, TResponse>`, `IRequestPreProcessor<TRequest>` and
  `IRequestPostProcessor<TRequest, TResponse>`; the `ISender`, `IPublisher`, `IStreamer` and `IMediator`
  dispatch surfaces. Every response is a `Boutquin.Domain` `Result` or `Result<T>`.
- `SenderExtensions.SendAsync<TResponse>(IRequest<TResponse>)`, which infers the response type from the request.
- **`Boutquin.Mediator`** — the mediator implementation and `AddMediator` registration, which scans assemblies with
  Scrutor for handlers, behaviors and processors. Individual `AddRequestHandlers`, `AddNotificationHandlers`,
  `AddStreamHandlers`, `AddBehaviors`, `AddPreProcessors` and `AddPostProcessors` methods are also provided.
  `IMediator` is registered as scoped, with `ISender`, `IPublisher` and `IStreamer` forwarding to the same
  per-scope instance, so handlers can depend on scoped services.
- Notification fan-out: `PublishAsync` runs every registered handler even when an earlier one fails, returns the
  first failure, and returns `Result.Success()` when no handler is registered.
- Streaming: `CreateStream` yields `IAsyncEnumerable<Result<T>>`, one `Result` per item, through stream
  pipeline behaviors.
- `LoggingPreProcessor<TRequest>`, a ready-made pre-processor that logs each request.
- **`Boutquin.Mediator.Authorization`** — `AuthorizationBehavior`, which enforces `[Authorize]` role and policy
  requirements and returns `Error.Unauthorized` or `Error.Forbidden` failures instead of throwing.
- **`Boutquin.Mediator.Caching`** — `CachingBehavior`, which caches `[CachingPolicy]` requests in
  `IDistributedCache` with absolute and sliding expiry, configuration-driven disable/reset, and optional per-user
  keys. Only success `Result`s are cached; a failure is returned but never stored. An anonymous caller under
  per-user caching bypasses the cache. A corrupt cache entry falls through to the handler.
- **`Boutquin.Mediator.Reliability`** — `RetryBehavior`, an opt-in, configuration-driven retry with per-attempt
  timeouts and a back-off schedule. `CachingPolicySettings` and `RetrySettings` bind from `IConfiguration`, and
  out-of-range values are coerced to safe defaults.
- **`Boutquin.Abstractions`** — `ICurrentUserService` (with claims-principal and Windows implementations),
  `IAuthorizationService`, and `AuthorizeAttribute`.
- **`Boutquin.Reliability`** — `IWaitWithRetry` / `WaitWithRetry` and `WaitSettings`: retry-with-timeout on Polly,
  usable without the mediator. Cancellation interrupts both the inter-retry sleep and the in-flight attempt.
- Public API baselines for all seven packages, enforced by `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
- XML documentation in every package, and a package-specific README for each NuGet package.
- SourceLink, deterministic builds, and symbol packages (`.snupkg`).

### Deprecated

- `SenderExtensions.SendAsync<TConcreteRequest>(ISender, TConcreteRequest, CancellationToken)`, the
  `dynamic`-returning overload, ships marked `[Obsolete]`. Use `SendAsync<TResponse>(IRequest<TResponse>)` or
  `ISender.SendAsync<TRequest, TResponse>`, which avoid runtime reflection and keep compile-time typing.

### Dependencies

- Targets .NET 10 and .NET 11, and is tested on both. Both targets depend on `Microsoft.Extensions.*` 11.0.0 and
  Polly 8.9.1. Depends on `Boutquin.Domain` at exactly 1.0.1, and on each sibling Boutquin.Mediator package at
  exactly the same version, so the family always restores as one set.
