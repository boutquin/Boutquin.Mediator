# Boutquin.Mediator.Abstractions

Contracts for the Boutquin mediator: requests, commands, queries, notifications, stream requests, their handlers, pipeline behaviors, and pre/post-processors. Every request returns a `Boutquin.Domain` `Result` (or `Result<T>`), so failure is part of the type rather than an exception.

Reference this package from the assemblies that define requests and handlers. Reference `Boutquin.Mediator` from the composition root that registers and dispatches them.

## Contracts

| Contract | Purpose |
|----------|---------|
| `IRequest<TResult>` | A message dispatched to exactly one handler. `TResult` must derive from `Result`. |
| `ICommand` / `ICommand<TResult>` | A state-changing request. `ICommand` returns a bare `Result`. |
| `IQuery<TResult>` | A read-only request. |
| `IRequestHandler<TRequest, TResponse>`, `ICommandHandler<…>`, `IQueryHandler<…>` | Handles one request type. |
| `INotificationHandler<TNotification>` | One of any number of handlers for an `INotification` (defined in `Boutquin.Domain`). |
| `IStreamRequest<T>` / `IStreamRequestHandler<TRequest, T>` | A request whose handler yields `IAsyncEnumerable<Result<T>>`, one `Result` per item. |
| `IPipelineBehavior<TRequest, TResponse>` | Wraps request handling; call `next()` to continue, or return a `Result` to short-circuit. |
| `IStreamPipelineBehavior<TRequest, T>` | The streaming equivalent of a pipeline behavior. |
| `IRequestPreProcessor<TRequest>` | Runs before the behaviors; a failed `Result` stops the request. |
| `IRequestPostProcessor<TRequest, TResponse>` | Runs after the behaviors and handler; a failed `Result` replaces the response. |
| `ISender`, `IPublisher`, `IStreamer`, `IMediator` | Dispatch surfaces. `IMediator` combines all three. |

## Example

```csharp
using Boutquin.Domain.Abstractions;
using Boutquin.Mediator.Abstractions;

public sealed record GetOrder(int Id) : IQuery<Result<OrderDto>>;

public sealed class GetOrderHandler(IOrderRepository orders)
    : IQueryHandler<GetOrder, Result<OrderDto>>
{
    public async ValueTask<Result<OrderDto>> HandleAsync(GetOrder query, CancellationToken cancellationToken = default)
    {
        var order = await orders.FindAsync(query.Id, cancellationToken);
        return order is null
            ? Result.Failure<OrderDto>(Error.NotFound("Order.NotFound", $"Order {query.Id} was not found."))
            : Result.Success(order.ToDto());
    }
}
```

`SenderExtensions.SendAsync(request)` infers the response type from the request, so callers write `await sender.SendAsync(new GetOrder(42))`.

## Related packages

- `Boutquin.Mediator` — the implementation and DI registration.
- `Boutquin.Mediator.Authorization`, `Boutquin.Mediator.Caching`, `Boutquin.Mediator.Reliability` — ready-made pipeline behaviors.

Source, architecture notes and changelog: https://github.com/boutquin/Boutquin.Mediator
