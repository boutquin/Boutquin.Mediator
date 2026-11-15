# Boutquin.Mediator

The mediator implementation for `Boutquin.Mediator.Abstractions`: request dispatch to a single handler, notification fan-out to every handler, streaming, pipeline behaviors, and pre/post-processors, with handler discovery through Scrutor assembly scanning.

## Registration

```csharp
using Boutquin.Mediator.Extensions;

services.AddMediator(typeof(GetOrderHandler).Assembly);
```

`AddMediator` registers `IMediator` (and `ISender`, `IPublisher`, `IStreamer`, all forwarding to the same instance) as **scoped**, then scans the given assemblies — or the calling assembly when none are given — for handlers, notification handlers, stream handlers, pipeline behaviors and pre/post-processors, registering each as transient. The individual `AddRequestHandlers`, `AddNotificationHandlers`, `AddStreamHandlers`, `AddBehaviors`, `AddPreProcessors` and `AddPostProcessors` methods are available when you want to register one kind at a time.

To use a behavior from another package, include that package's assembly in the scan:

```csharp
services.AddMediator(
    typeof(GetOrderHandler).Assembly,
    typeof(Boutquin.Mediator.Authorization.AuthorizationBehavior<,>).Assembly);
```

## Dispatch

```csharp
public sealed class OrdersEndpoint(ISender sender, IPublisher publisher)
{
    public async Task<IResult> Get(int id)
    {
        var result = await sender.SendAsync(new GetOrder(id));
        return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
    }

    public ValueTask<Result> Placed(OrderPlaced evt) => publisher.PublishAsync(evt);
}
```

## Semantics

- **Send** resolves exactly one handler; a missing handler throws `InvalidOperationException`. Pre-processors run first and a failed `Result` stops the request. Pipeline behaviors then wrap the handler, the first registered behavior outermost. Post-processors run last; the first failure replaces the response.
- **Publish** invokes every registered handler even when an earlier one fails, and returns the first failure encountered (or `Result.Success()`, including when no handler is registered).
- **CreateStream** resolves one stream handler, wraps it in any stream pipeline behaviors, and yields one `Result<T>` per item.
- **Cancellation** observed anywhere in the pipeline propagates to the caller as `OperationCanceledException` rather than being converted into a failure `Result`.
- `LoggingPreProcessor<TRequest>` is a ready-made pre-processor that logs each request at debug level.

Source, architecture notes and changelog: https://github.com/boutquin/Boutquin.Mediator
