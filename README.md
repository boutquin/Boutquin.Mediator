# Boutquin.Mediator

![NuGet](https://img.shields.io/nuget/vpre/Boutquin.Mediator?style=for-the-badge) ![License](https://img.shields.io/github/license/boutquin/Boutquin.Mediator?style=for-the-badge)

A .NET 10 and .NET 11 mediator library family: request dispatch, notification fan-out and streaming over a `Result`-based contract, with pipeline behaviors for authorization, distributed caching, and retry.

Every request returns a `Boutquin.Domain` `Result` or `Result<T>`. Expected failures — not found, forbidden, a validation error — travel as values through the pipeline instead of as exceptions, and the behaviors use the same contract to short-circuit.

## Solution Structure

| Project | NuGet Package | Description |
|---------|---------------|-------------|
| **Mediator.Abstractions** | [`Boutquin.Mediator.Abstractions`](https://www.nuget.org/packages/Boutquin.Mediator.Abstractions) | Request, command, query, notification, stream and pipeline contracts |
| **Mediator** | [`Boutquin.Mediator`](https://www.nuget.org/packages/Boutquin.Mediator) | Mediator implementation and DI registration with Scrutor handler discovery |
| **Mediator.Authorization** | [`Boutquin.Mediator.Authorization`](https://www.nuget.org/packages/Boutquin.Mediator.Authorization) | `[Authorize]` role and policy enforcement behavior |
| **Mediator.Caching** | [`Boutquin.Mediator.Caching`](https://www.nuget.org/packages/Boutquin.Mediator.Caching) | `IDistributedCache` response-caching behavior, optionally per user |
| **Mediator.Reliability** | [`Boutquin.Mediator.Reliability`](https://www.nuget.org/packages/Boutquin.Mediator.Reliability) | Configuration-driven retry behavior with per-attempt timeouts |
| **Abstractions** | [`Boutquin.Abstractions`](https://www.nuget.org/packages/Boutquin.Abstractions) | Current-user and authorization-service abstractions |
| **Reliability** | [`Boutquin.Reliability`](https://www.nuget.org/packages/Boutquin.Reliability) | Retry-with-timeout primitives on Polly, usable without the mediator |
| **Tests.UnitTests** | — | xUnit unit tests |
| **Mediator.Tests.IntegrationTests** | — | xUnit tests through a real DI container and pipeline |

## Supported target frameworks

Every package targets both `net10.0` and `net11.0`. Both targets depend on the `Microsoft.Extensions.*` 11.0.0 packages, which support .NET 10. To build, use the .NET 11 SDK that `global.json` selects. To run the full test suite, install both the .NET 10 and .NET 11 runtimes.

## Features

- **Request/response** — `ISender.SendAsync` dispatches to exactly one handler; commands (`ICommand`) and queries (`IQuery<T>`) are typed variants of `IRequest<T>`.
- **Notification fan-out** — `IPublisher.PublishAsync` runs every handler even when an earlier one fails and returns the first failure.
- **Streaming** — `IStreamer.CreateStream` yields `IAsyncEnumerable<Result<T>>`, one `Result` per item, with stream pipeline behaviors.
- **Pipeline** — pre-processors, pipeline behaviors (first registered is outermost), the handler, then post-processors; any stage can return a failure `Result`.
- **Authorization** — `[Authorize(RequiredRoles = …, Policy = …)]` on a request type, checked against your `IAuthorizationService`; unauthenticated and forbidden callers get a failure `Result`.
- **Caching** — `[CachingPolicy]` request types are cached in `IDistributedCache` with absolute and sliding expiry, optional per-user keys, and configuration-driven reset/disable.
- **Retry** — opt-in per request type from configuration: retry count, per-attempt timeout, back-off schedule.
- **Scoped mediator** — `IMediator` is registered per scope, so handlers can depend on scoped services such as a `DbContext`.

## Quick Start

```csharp
using Boutquin.Domain.Abstractions;
using Boutquin.Mediator.Abstractions;
using Boutquin.Mediator.Abstractions.Extensions;
using Boutquin.Mediator.Extensions;

// 1. Define a query and its handler.
public sealed record GetOrder(int Id) : IQuery<Result<OrderDto>>;

public sealed class GetOrderHandler(IOrderRepository orders) : IQueryHandler<GetOrder, Result<OrderDto>>
{
    public async ValueTask<Result<OrderDto>> HandleAsync(GetOrder query, CancellationToken cancellationToken = default)
    {
        var order = await orders.FindAsync(query.Id, cancellationToken);
        return order is null
            ? Result.Failure<OrderDto>(Error.NotFound("Order.NotFound", $"Order {query.Id} was not found."))
            : Result.Success(order.ToDto());
    }
}

// 2. Register handlers, plus any behavior packages you want in the pipeline.
builder.Services.AddMediator(
    typeof(GetOrderHandler).Assembly,
    typeof(Boutquin.Mediator.Authorization.AuthorizationBehavior<,>).Assembly);

// 3. Send it.
app.MapGet("/orders/{id:int}", async (int id, ISender sender) =>
{
    var result = await sender.SendAsync(new GetOrder(id));
    return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
});
```

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for the package layers, the dispatch pipeline, and how the behaviors read their configuration.

## Package documentation

Each NuGet package carries its own README covering the APIs it ships, its configuration keys and its registration. This README is the family overview.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on reporting bugs, suggesting enhancements, and submitting pull requests. This project adheres to the Contributor Covenant [Code of Conduct](CODE_OF_CONDUCT.md). Report vulnerabilities as described in [SECURITY.md](SECURITY.md).

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE file](LICENSE.txt) for more information.
