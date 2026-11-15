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
// Boutquin.Mediator.Tests
using Common;
using Extensions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Verifies that <c>ServiceCollectionExtensions.AddMediator</c> registers the segregated
/// interfaces <see cref="ISender"/>, <see cref="IPublisher"/>, and <see cref="IStreamer"/> as
/// first-class service keys, and that within a scope all four keys resolve to the same instance.
/// </summary>
/// <remarks>
/// Guards against the gap fixed by F-X2-1: prior to the fix, only <see cref="IMediator"/> was
/// registered, so a consumer with a constructor dependency on the narrower <see cref="ISender"/>
/// interface — the natural way to use <see cref="Abstractions.Extensions.SenderExtensions"/> —
/// received <see cref="InvalidOperationException"/> at resolution time (or at
/// <see cref="ServiceProvider"/> build time when <c>ValidateOnBuild=true</c>).
/// </remarks>
public sealed class SegregatedInterfaceRegistrationTests
{
    /// <summary>
    /// Verifies that <see cref="ISender"/> can be resolved directly from the DI container
    /// (via <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/>) after
    /// calling <c>ServiceCollectionExtensions.AddMediator</c>.
    /// </summary>
    [Fact]
    public void AddMediator_ISender_ResolvesFromDI()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        // Act & Assert — resolves without InvalidOperationException
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Assert.NotNull(sender);
    }

    /// <summary>
    /// Verifies that <see cref="IPublisher"/> can be resolved directly from the DI container
    /// after calling <c>ServiceCollectionExtensions.AddMediator</c>.
    /// </summary>
    [Fact]
    public void AddMediator_IPublisher_ResolvesFromDI()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        // Act & Assert
        using var scope = sp.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        Assert.NotNull(publisher);
    }

    /// <summary>
    /// Verifies that <see cref="IStreamer"/> can be resolved directly from the DI container
    /// after calling <c>ServiceCollectionExtensions.AddMediator</c>.
    /// </summary>
    [Fact]
    public void AddMediator_IStreamer_ResolvesFromDI()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        // Act & Assert
        using var scope = sp.CreateScope();
        var streamer = scope.ServiceProvider.GetRequiredService<IStreamer>();
        Assert.NotNull(streamer);
    }

    /// <summary>
    /// Verifies that <see cref="ISender"/>, <see cref="IPublisher"/>, <see cref="IStreamer"/>, and
    /// <see cref="IMediator"/> all resolve to the SAME per-scope instance — ensuring no duplicate
    /// mediator is created and that all four keys share one object within the scope.
    /// </summary>
    [Fact]
    public void AddMediator_SegregatedInterfaces_SameInstanceAsIMediator()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        // Act
        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var streamer = scope.ServiceProvider.GetRequiredService<IStreamer>();

        // Assert — all four keys must resolve to the SAME per-scope mediator instance
        Assert.Same(mediator, sender);
        Assert.Same(mediator, publisher);
        Assert.Same(mediator, streamer);
    }

    /// <summary>
    /// Verifies that <c>ValidateOnBuild = true</c> (the .NET 6+ hosted-app default) does NOT
    /// surface a startup validation error for a consumer that has a constructor dependency on
    /// <see cref="ISender"/>, confirming the ISender registration is present at build time.
    /// </summary>
    /// <remarks>
    /// Uses a minimal service collection with no assembly scan (to avoid triggering unresolvable
    /// transient handler dependencies) — the ISender registration is the only thing under test.
    /// </remarks>
    [Fact]
    public void AddMediator_ValidateOnBuild_ISenderConsumerRegistersWithoutError()
    {
        // Arrange — minimal collection: AddMediator without an assembly scan avoids triggering
        // ValidateOnBuild errors for handlers whose logger dependencies aren't registered.
        var services = new ServiceCollection();
        _ = services.AddMediator(); // no assembly; registers IMediator + the three forwarding keys only

        // Register a consumer whose constructor demands ISender — ValidateOnBuild will
        // catch a missing ISender registration before any call is made.
        _ = services.AddScoped<ISenderConsumer>();

        // Act & Assert — must NOT throw InvalidOperationException
        using var sp = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = sp.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<ISenderConsumer>();
        Assert.NotNull(consumer);
    }

    /// <summary>A minimal consumer whose constructor declares a dependency on <see cref="ISender"/>.</summary>
    private sealed class ISenderConsumer(ISender sender)
    {
        public ISender Sender { get; } = sender;
    }
}
