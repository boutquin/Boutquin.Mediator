// Copyright (c) 2025-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Mediator.Helpers;

// Boutquin.Mediator
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Abstractions;
using Boutquin.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides a mediator for sending requests, publishing notifications, and dispatching stream requests through a processing pipeline.
/// </summary>
/// <remarks>The <see cref="Mediator"/> class serves as a central point for coordinating request/response,
/// notification handling, and streaming in an application. It uses dependency injection to resolve handlers,
/// preprocessors, post-processors, and pipeline behaviors for requests and notifications. This class ensures that
/// requests and notifications are processed in a structured and extensible manner.</remarks>
/// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve services. Cannot be <see langword="null"/>.</param>
public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    /// <summary>
    /// Provides access to application services through dependency injection.
    /// </summary>
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Cache of <see cref="MethodInfo"/> for <c>Result.Failure&lt;T&gt;</c> indexed by the value type <c>T</c>,
    /// avoiding repeated <see cref="MethodInfo.MakeGenericMethod(Type[])"/> allocations on the hot path.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_failureMethodCache = new();

    /// <summary>
    /// Sends a request through the processing pipeline and returns the corresponding response.
    /// </summary>
    /// <remarks>This method processes the request using the following steps: <list type="number">
    /// <item><description>Retrieves the appropriate request handler for the specified <typeparamref
    /// name="TRequest"/>.</description></item> <item><description>Executes all registered pre-processors for the
    /// request. If any pre-processor fails, the method returns its result immediately.</description></item>
    /// <item><description>Applies all registered pipeline behaviors in reverse order, wrapping the request handler
    /// execution.</description></item> <item><description>Invokes the request handler to process the request and
    /// produce the response.</description></item>
    /// <item><description>Executes all registered post-processors. If any post-processor returns a failure,
    /// the method returns that failure immediately.</description></item></list></remarks>
    /// <typeparam name="TRequest">The type of the request to be sent. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected. Must inherit from <see cref="Result"/>.</typeparam>
    /// <param name="request">The request object to be processed. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation. Defaults to <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the response of
    /// type <typeparamref name="TResponse"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the specified request type <typeparamref name="TRequest"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is signaled before completion. Cancellation observed by a pre-processor, pipeline behavior, the handler, or a post-processor propagates to the caller unwrapped rather than being mapped to a failure <see cref="Result"/>.</exception>
    public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
        where TResponse : Result
    {
        ArgumentNullException.ThrowIfNull(request);

        // Fail fast no handler registered for request type
        var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        // Pre-process the request using all registered pre-processors
        var preprocessors = _serviceProvider.GetServices<IRequestPreProcessor<TRequest>>();
        foreach (var preprocessor in preprocessors)
        {
            var result = await preprocessor.ProcessAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return CreateFailureResponse<TResponse>(result.Error);
            }
        }

        // Apply all registered pipeline behaviors in reverse order
        var behaviors = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse();
        // Create a delegate that represents the current handler
        var current = () => handler.HandleAsync(request, cancellationToken);
        // Wrap the handler with each behavior in the pipeline
        foreach (var behavior in behaviors)
        {
            var next = current;
            current = () => behavior.HandleAsync(request, next, cancellationToken);
        }

        // Execute the final handler wrapped in the pipeline behaviors
        var response = await current().ConfigureAwait(false);

        // Run all post-processors after the handler; propagate the first failure
        var postProcessors = _serviceProvider.GetServices<IRequestPostProcessor<TRequest, TResponse>>();
        foreach (var postProcessor in postProcessors)
        {
            var postResult = await postProcessor.ProcessAsync(request, response, cancellationToken).ConfigureAwait(false);
            if (!postResult.IsSuccess)
            {
                return CreateFailureResponse<TResponse>(postResult.Error);
            }
        }

        return response;
    }

    /// <summary>
    /// Publishes a notification to all registered handlers asynchronously.
    /// </summary>
    /// <remarks>
    /// This method resolves all registered <see cref="INotificationHandler{TNotification}"/> instances from
    /// the service provider and invokes each handler sequentially. All handlers are always called regardless of
    /// whether a previous handler returned an error or threw a non-cancellation exception (fan-out semantics).
    /// If multiple handlers fail, only the first encountered error is returned in the result. If no handlers are
    /// registered, the method returns <see cref="Result.Success()"/> rather than throwing.
    /// <para>
    /// Cooperative cancellation is an exception to the fan-out: if a handler observes the signaled
    /// <paramref name="cancellationToken"/> and throws <see cref="OperationCanceledException"/> (including
    /// <see cref="System.Threading.Tasks.TaskCanceledException"/>), that exception propagates to the caller
    /// unwrapped and aborts the remaining fan-out — it is not captured into the aggregate failure
    /// <see cref="Result"/>. Only non-cancellation exceptions are mapped to an
    /// <see cref="Error.InternalServerError(string, string)"/> entry.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNotification">The type of the notification to be published. Must implement <see cref="INotification"/>.</typeparam>
    /// <param name="notification">The notification instance to be handled. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation. Defaults to <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, containing a <see cref="Result"/>
    /// that indicates the aggregate outcome of all notification handlers. Returns <see cref="Result.Success()"/> when
    /// all handlers succeed or no handlers are registered; returns the first error encountered when one or more
    /// handlers fail or throw a non-cancellation exception.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="notification"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown if a handler observes the signaled <paramref name="cancellationToken"/> and cancels; cancellation propagates to the caller and aborts the remaining fan-out rather than being captured into the aggregate failure <see cref="Result"/>.</exception>
    public async ValueTask<Result> PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();
        var errors = new List<Error>();

        foreach (var handler in handlers)
        {
            try
            {
                var result = await handler.HandleAsync(notification, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    errors.Add(result.Error);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add(Error.InternalServerError(
                    "Error.NotificationHandler.Exception",
                    ex.Message));
            }
        }

        return errors.Count == 0
            ? Result.Success()
            : Result.Failure(errors[0]);
    }

    /// <summary>
    /// Dispatches a stream request to its handler, wrapping it in any registered stream pipeline behaviors,
    /// and returns an asynchronous sequence of <see cref="Result{TValue}"/> items.
    /// </summary>
    /// <remarks>
    /// This method resolves the registered <see cref="IStreamRequestHandler{TRequest,TResponse}"/> from the
    /// dependency injection container, applies any registered <see cref="IStreamPipelineBehavior{TRequest,TResponse}"/>
    /// instances in reverse registration order (outermost first), and returns the composed asynchronous sequence.
    /// No <see langword="async"/>/<see langword="await"/> is used here; the method delegates synchronously to
    /// the resolved handler pipeline.
    /// </remarks>
    /// <typeparam name="TRequest">The type of the stream request. Must implement <see cref="IStreamRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The element type returned in each <see cref="Result{TValue}"/> item of the stream.</typeparam>
    /// <param name="request">The stream request to dispatch. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation. Defaults to <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="Result{TValue}"/> items produced by the handler pipeline.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the specified stream request type <typeparamref name="TRequest"/>.</exception>
    public IAsyncEnumerable<Result<TResponse>> CreateStream<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IStreamRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);

        var handler = _serviceProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
        var behaviors = _serviceProvider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>().Reverse();

        StreamHandlerDelegate<TResponse> current = () => handler.HandleAsync(request, cancellationToken);
        foreach (var behavior in behaviors)
        {
            var next = current;
            current = () => behavior.HandleAsync(request, next, cancellationToken);
        }

        return current();
    }

    /// <summary>
    /// Creates a typed failure response of <typeparamref name="TResponse"/>, which may be <see cref="Result"/>
    /// or <see cref="Result{T}"/>, using the cached generic factory method when needed.
    /// </summary>
    private static TResponse CreateFailureResponse<TResponse>(Error error) where TResponse : Result
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var failureMethod = s_failureMethodCache.GetOrAdd(
            valueType,
            t => typeof(Result).GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!.MakeGenericMethod(t));
        return (TResponse)failureMethod.Invoke(null, [error])!;
    }
}
