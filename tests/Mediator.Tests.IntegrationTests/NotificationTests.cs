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
/// Integration tests for additional <see cref="IPublisher.PublishAsync{TNotification}"/> dispatch behaviour.
/// </summary>
/// <remarks>
/// Covers CR-16: cancellation token propagation, notification-type isolation, and
/// all-handlers-run-even-after-failure fan-out semantics.
/// The basic fan-out and error-aggregation scenarios live in <see cref="PublishTests"/>.
/// </remarks>
public sealed class NotificationTests
{
    // -----------------------------------------------------------------------
    // Test notification types
    // -----------------------------------------------------------------------

    /// <summary>Primary notification type used for CR-16 dispatch tests.</summary>
    private sealed record PingNotification(string Payload) : INotification;

    /// <summary>A second, unrelated notification type used to verify type isolation.</summary>
    private sealed record OtherNotification(string Payload) : INotification;

    // -----------------------------------------------------------------------
    // CR-16: cancellation token is forwarded to handlers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="IPublisher.PublishAsync{TNotification}"/> forwards the supplied
    /// <see cref="CancellationToken"/> to each registered notification handler.
    /// </summary>
    [Fact]
    public async Task PublishAsync_ForwardsCancellationToken_ToHandlers()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        CancellationToken capturedToken = default;
        using var cts = new CancellationTokenSource();

        var handler = new DelegateNotificationHandler<PingNotification>((_, ct) =>
        {
            capturedToken = ct;
            return ValueTask.FromResult(Result.Success());
        });

        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(PingNotification).Assembly);
        _ = services.AddSingleton<INotificationHandler<PingNotification>>(handler);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.PublishAsync(new PingNotification("ct-test"), cts.Token).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess, "PublishAsync must succeed when the handler succeeds.");
        Assert.Equal(cts.Token, capturedToken);
    }

    // -----------------------------------------------------------------------
    // CR-16: notification-type isolation — handlers for OtherNotification
    //        must NOT be invoked when PingNotification is published
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that publishing a <see cref="PingNotification"/> does not invoke handlers
    /// registered for <see cref="OtherNotification"/> (notification-type isolation).
    /// </summary>
    [Fact]
    public async Task PublishAsync_TypeIsolation_DoesNotInvokeHandlersForOtherType()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var otherHandlerCalled = false;

        var otherHandler = new DelegateNotificationHandler<OtherNotification>((_, _) =>
        {
            otherHandlerCalled = true;
            return ValueTask.FromResult(Result.Success());
        });

        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(PingNotification).Assembly);
        // Register a handler ONLY for OtherNotification
        _ = services.AddSingleton<INotificationHandler<OtherNotification>>(otherHandler);
        // No handler for PingNotification

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.PublishAsync(new PingNotification("isolation-test")).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess, "PublishAsync with no handlers for the published type must succeed.");
        Assert.False(otherHandlerCalled, "Handler for OtherNotification must NOT be called when PingNotification is published.");
    }

    // -----------------------------------------------------------------------
    // CR-16: all handlers are invoked even when the first handler fails
    //        (fan-out semantics — matches the documented implementation in Mediator.cs)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="IPublisher.PublishAsync{TNotification}"/> continues to invoke
    /// subsequent handlers even when an earlier handler returns a failure, and that the first
    /// error is reflected in the aggregate result.
    /// </summary>
    [Fact]
    public async Task PublishAsync_AllHandlersInvoked_EvenWhenFirstFails()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var firstHandlerCalled = false;
        var secondHandlerCalled = false;
        var firstError = Error.BadRequest("Notify.First", "First handler failed");

        var firstHandler = new DelegateNotificationHandler<PingNotification>((_, _) =>
        {
            firstHandlerCalled = true;
            return ValueTask.FromResult<Result>(firstError);
        });

        var secondHandler = new DelegateNotificationHandler<PingNotification>((_, _) =>
        {
            secondHandlerCalled = true;
            return ValueTask.FromResult(Result.Success());
        });

        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(PingNotification).Assembly);
        _ = services.AddSingleton<INotificationHandler<PingNotification>>(firstHandler);
        _ = services.AddSingleton<INotificationHandler<PingNotification>>(secondHandler);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.PublishAsync(new PingNotification("fan-out-failure")).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(firstHandlerCalled, "First handler must have been called.");
        Assert.True(secondHandlerCalled, "Second handler must have been called even though the first failed.");
        Assert.False(result.IsSuccess, "Aggregate result must be failure when any handler fails.");
        Assert.NotNull(result.Error);
        Assert.Equal(firstError.Code, result.Error.Code);
    }

    // -----------------------------------------------------------------------
    // S9-01: handler that THROWS — remaining handlers still run (fan-out)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when one notification handler throws an exception, the remaining
    /// handlers still execute (fan-out semantics) and the aggregate result is a failure.
    /// </summary>
    [Fact]
    public async Task PublishAsync_AllHandlersInvoked_EvenWhenFirstHandlerThrows()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var secondHandlerCalled = false;

        var throwingHandler = new DelegateNotificationHandler<PingNotification>((_, _) =>
            throw new InvalidOperationException("Handler exploded"));

        var secondHandler = new DelegateNotificationHandler<PingNotification>((_, _) =>
        {
            secondHandlerCalled = true;
            return ValueTask.FromResult(Result.Success());
        });

        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(PingNotification).Assembly);
        _ = services.AddSingleton<INotificationHandler<PingNotification>>(throwingHandler);
        _ = services.AddSingleton<INotificationHandler<PingNotification>>(secondHandler);

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.PublishAsync(new PingNotification("throw-fanout")).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(secondHandlerCalled,
            "The second handler must still run even though the first handler threw an exception.");
        Assert.False(result.IsSuccess,
            "Aggregate result must be failure when a handler throws.");
        Assert.Equal("Error.NotificationHandler.Exception", result.Error.Code);
    }

    // -----------------------------------------------------------------------
    // Helper: delegate-based notification handler (avoids needing Moq here)
    // -----------------------------------------------------------------------

    private sealed class DelegateNotificationHandler<TNotification>(
        Func<TNotification, CancellationToken, ValueTask<Result>> handle)
        : INotificationHandler<TNotification>
        where TNotification : INotification
    {
        public ValueTask<Result> HandleAsync(TNotification notification, CancellationToken cancellationToken = default)
            => handle(notification, cancellationToken);
    }
}
