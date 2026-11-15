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
using Abstractions.Extensions;
using Boutquin.Domain.Abstractions;
// Boutquin.Mediator.Tests
using Common;
using Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Integration tests for the dynamic <c>SendAsync&lt;TConcreteRequest&gt;</c> overload
/// defined in <see cref="SenderExtensions"/>.
/// </summary>
/// <remarks>
/// Covers CR-17: null-guard, no-interface error, valid dispatch, and null-sender guard.
/// CS0618 is suppressed at the project level so the <c>[Obsolete]</c> attribute on
/// <c>SenderExtensions.SendAsync&lt;TConcreteRequest&gt;</c> does not cause build errors here.
/// </remarks>
public sealed class SenderExtensionsTests
{
    // -----------------------------------------------------------------------
    // CR-17: null sender → ArgumentNullException
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that passing a <see langword="null"/> sender to
    /// <c>SenderExtensions.SendAsync&lt;TConcreteRequest&gt;</c> throws
    /// <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task SendAsync_NullSender_ThrowsArgumentNullException()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        ISender? nullSender = null;
        var request = new SimpleCommand.Command("test");

        //---------------------------------------------------------
        // Act & Assert
        //---------------------------------------------------------
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await nullSender!.SendAsync(request, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
    }

    // -----------------------------------------------------------------------
    // CR-17: null request → ArgumentNullException
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that passing a <see langword="null"/> request to
    /// <c>SenderExtensions.SendAsync&lt;TConcreteRequest&gt;</c> throws
    /// <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        ISender sender = serviceProvider.GetRequiredService<IMediator>();

        SimpleCommand.Command? nullRequest = null;

        //---------------------------------------------------------
        // Act & Assert
        //---------------------------------------------------------
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sender.SendAsync(nullRequest!, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
    }

    // -----------------------------------------------------------------------
    // CR-17: request does not implement IRequest<TResponse> → InvalidOperationException
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when a type that does not implement <c>IRequest&lt;TResponse&gt;</c>
    /// is sent via the dynamic overload, an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    [Fact]
    public async Task SendAsync_TypeWithoutIRequestInterface_ThrowsInvalidOperationException()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        ISender sender = serviceProvider.GetRequiredService<IMediator>();

        // NotARequest does not implement IRequest<TResponse>
        var badRequest = new NotARequest();

        //---------------------------------------------------------
        // Act & Assert
        //---------------------------------------------------------
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sender.SendAsync(badRequest, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
    }

    // -----------------------------------------------------------------------
    // CR-17: valid request → response returned (success path)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that sending a valid request via the dynamic
    /// <c>SenderExtensions.SendAsync&lt;TConcreteRequest&gt;</c> overload returns
    /// the expected response, proving that dynamic dispatch works end-to-end.
    /// </summary>
    [Fact]
    public async Task SendAsync_ValidRequest_ReturnsExpectedResponse()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        _ = services.AddSingleton(Mock.Of<ILogger<SimpleCommand.Command>>());
        var serviceProvider = services.BuildServiceProvider();
        ISender sender = serviceProvider.GetRequiredService<IMediator>();

        var command = new SimpleCommand.Command("hello-dynamic");

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        dynamic result = await sender.SendAsync(command, CancellationToken.None).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(result);
        // The dynamic result is a Result (non-generic); verify it is a success.
        bool isSuccess = result.IsSuccess;
        Assert.True(isSuccess, "Dynamic SendAsync must return a success result for a valid command.");
    }

    // -----------------------------------------------------------------------
    // S9-03: Overload 1 dispatches correctly when variable typed as IRequest<TResponse>
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="SenderExtensions.SendAsync{TResponse}(ISender, IRequest{TResponse}, CancellationToken)"/>
    /// correctly resolves and dispatches to the handler even when the caller holds the request
    /// through the <see cref="IRequest{TResponse}"/> interface (not the concrete type).
    /// The previous implementation passed <c>IRequest&lt;TResponse&gt;</c> as the TRequest type argument,
    /// which caused DI resolution to fail.
    /// </summary>
    [Fact]
    public async Task SendAsync_TypedAsIRequest_DispatchesToConcreteHandler()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        var loggerMock = new Mock<ILogger<SimpleCommand.Command>>();
        _ = services.AddSingleton(loggerMock.Object);

        var serviceProvider = services.BuildServiceProvider();
        // IMediator implements ISender; resolve via IMediator (either key works — ISender is
        // also registered directly via forwarding since F-X2-1 was fixed).
        ISender sender = serviceProvider.GetRequiredService<IMediator>();

        // Hold request through the interface — forces overload 1 when explicit TResponse is provided
        IRequest<Result> request = new SimpleCommand.Command("interface-typed");

        //---------------------------------------------------------
        // Act — explicit <Result> forces the SendAsync<TResponse>(ISender, IRequest<TResponse>) overload
        //---------------------------------------------------------
        var result = await sender.SendAsync<Result>(request).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "Overload 1 must dispatch to the correct concrete handler.");
    }

    // -----------------------------------------------------------------------
    // Helper: plain class that does NOT implement IRequest<TResponse>
    // -----------------------------------------------------------------------

    private sealed class NotARequest
    {
        public string Value { get; } = "not-a-request";
    }
}
