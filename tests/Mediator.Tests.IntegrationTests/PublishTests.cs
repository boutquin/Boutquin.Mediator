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

namespace Boutquin.Mediator.Tests.IntegrationTests;

// Boutquin.Mediator
using Abstractions;
using Boutquin.Domain.Abstractions;
// Boutquin.Mediator.Tests
using Extensions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for <see cref="IPublisher.PublishAsync{TNotification}"/> dispatch behaviour.
/// </summary>
/// <remarks>
/// Covers CR-04 (fan-out to all registered handlers) and CR-08 (error aggregation / zero-handler
/// success contract).
/// </remarks>
public sealed class PublishTests
{
    // -----------------------------------------------------------------------
    // Test notification type
    // -----------------------------------------------------------------------

    /// <summary>Simple marker notification used throughout this test class.</summary>
    private sealed record TestNotification(string Message) : INotification;

    // -----------------------------------------------------------------------
    // CR-04: fan-out — all registered handlers must be invoked
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="IPublisher.PublishAsync{TNotification}"/> dispatches the notification
    /// to ALL registered handlers (not just the first one).
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithTwoHandlers_InvokesBothHandlers()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var handler1Called = false;
        var handler2Called = false;

        var handler1 = new DelegateNotificationHandler<TestNotification>(_ =>
        {
            handler1Called = true;
            return ValueTask.FromResult(Result.Success());
        });

        var handler2 = new DelegateNotificationHandler<TestNotification>(_ =>
        {
            handler2Called = true;
            return ValueTask.FromResult(Result.Success());
        });

        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(TestNotification).Assembly);
        _ = services.AddSingleton<INotificationHandler<TestNotification>>(handler1);
        _ = services.AddSingleton<INotificationHandler<TestNotification>>(handler2);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var notification = new TestNotification("hello");

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.PublishAsync(notification);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess, "PublishAsync with two successful handlers must succeed.");
        Assert.True(handler1Called, "Handler 1 must have been invoked.");
        Assert.True(handler2Called, "Handler 2 must have been invoked.");
    }

    // -----------------------------------------------------------------------
    // CR-08 / Decision Rule 4: error aggregation — first error is returned
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when one handler succeeds and one fails, <see cref="IPublisher.PublishAsync{TNotification}"/>
    /// returns a failure result containing the first encountered error.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenOneHandlerFails_ReturnsFirstError()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var successHandlerCalled = false;
        var failureHandlerCalled = false;

        var expectedError = Error.BadRequest("Notify.Failed", "First handler failure");

        var failingHandler = new DelegateNotificationHandler<TestNotification>(_ =>
        {
            failureHandlerCalled = true;
            return ValueTask.FromResult<Result>(expectedError);
        });

        var succeedingHandler = new DelegateNotificationHandler<TestNotification>(_ =>
        {
            successHandlerCalled = true;
            return ValueTask.FromResult(Result.Success());
        });

        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(TestNotification).Assembly);
        // Register failing handler first so it is the "first error"
        _ = services.AddSingleton<INotificationHandler<TestNotification>>(failingHandler);
        _ = services.AddSingleton<INotificationHandler<TestNotification>>(succeedingHandler);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var notification = new TestNotification("fail-first");

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.PublishAsync(notification);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.False(result.IsSuccess, "PublishAsync must return failure when any handler fails.");
        Assert.True(failureHandlerCalled, "The failing handler must have been invoked.");
        Assert.True(successHandlerCalled, "The succeeding handler must have been invoked (fan-out).");
        Assert.NotNull(result.Error);
        Assert.Equal(expectedError.Code, result.Error.Code);
    }

    // -----------------------------------------------------------------------
    // CR-08 / Decision Rule 5: zero handlers → Result.Success()
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that publishing a notification when no handlers are registered returns
    /// <see cref="Result.Success()"/> rather than throwing.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithNoHandlers_ReturnsSuccess()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(TestNotification).Assembly);
        // Intentionally register NO handlers for TestNotification

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var notification = new TestNotification("orphan");

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.PublishAsync(notification);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess, "PublishAsync with no handlers must return Result.Success().");
    }

    // -----------------------------------------------------------------------
    // Cancellation: cooperative cancellation must PROPAGATE, not be swallowed
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when a notification handler observes a signaled <see cref="CancellationToken"/> and
    /// throws <see cref="OperationCanceledException"/>, that cancellation PROPAGATES out of
    /// <see cref="IPublisher.PublishAsync{TNotification}"/> rather than being swallowed into a failure
    /// <see cref="Result"/> and the fan-out continuing.
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenHandlerObservesCancellation_PropagatesOperationCanceledException()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var secondHandlerCalled = false;

        var cancellingHandler = new CancellationObservingHandler<TestNotification>();

        var secondHandler = new DelegateNotificationHandler<TestNotification>(_ =>
        {
            secondHandlerCalled = true;
            return ValueTask.FromResult(Result.Success());
        });

        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(TestNotification).Assembly);
        _ = services.AddSingleton<INotificationHandler<TestNotification>>(cancellingHandler);
        _ = services.AddSingleton<INotificationHandler<TestNotification>>(secondHandler);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var notification = new TestNotification("cancelled");

        //---------------------------------------------------------
        // Act + Assert
        //---------------------------------------------------------
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await mediator.PublishAsync(notification, cts.Token).ConfigureAwait(true));

        Assert.False(
            secondHandlerCalled,
            "Cancellation must abort the fan-out — the second handler must not run.");
    }

    // -----------------------------------------------------------------------
    // Helper: delegate-based notification handler (avoids needing Moq for this)
    // -----------------------------------------------------------------------

    private sealed class DelegateNotificationHandler<TNotification>(
        Func<TNotification, ValueTask<Result>> handle)
        : INotificationHandler<TNotification>
        where TNotification : INotification
    {
        public ValueTask<Result> HandleAsync(TNotification notification, CancellationToken cancellationToken = default)
            => handle(notification);
    }

    /// <summary>
    /// A notification handler that observes the cancellation token and throws
    /// <see cref="OperationCanceledException"/> when it is signaled (cooperative cancellation).
    /// </summary>
    private sealed class CancellationObservingHandler<TNotification>
        : INotificationHandler<TNotification>
        where TNotification : INotification
    {
        public ValueTask<Result> HandleAsync(TNotification notification, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result.Success());
        }
    }
}
