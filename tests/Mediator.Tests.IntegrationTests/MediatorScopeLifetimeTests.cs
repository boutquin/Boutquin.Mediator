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
/// Integration tests pinning the mediator's DI lifetime contract: the mediator must resolve its
/// collaborators from the <em>per-request</em> scope, not the root scope, so that handlers (and their
/// transitive dependencies) registered with a <see cref="ServiceLifetime.Scoped"/> lifetime resolve
/// correctly and transient <see cref="IDisposable"/> handlers are disposed at the end of each scope.
/// </summary>
/// <remarks>
/// Guards against the captive-dependency defect: a <see cref="ServiceLifetime.Singleton"/> mediator
/// captures the root <see cref="IServiceProvider"/>, so resolving a handler whose dependency is scoped
/// throws <see cref="InvalidOperationException"/> under <c>ValidateScopes=true</c> (the ASP.NET Core
/// development default), and transient disposables leak until the root provider is disposed.
/// </remarks>
public sealed class MediatorScopeLifetimeTests
{
    // -----------------------------------------------------------------------
    // Request + scoped dependency + handler
    // -----------------------------------------------------------------------

    /// <summary>A request whose handler depends on a scoped service.</summary>
    private sealed record ScopedDependencyRequest : IRequest<Result>;

    /// <summary>A service registered with a scoped lifetime — must only be resolvable inside a scope.</summary>
    private sealed class ScopedDependency
    {
        public bool Touched { get; private set; }

        public void Touch() => Touched = true;
    }

    /// <summary>Handler that consumes the scoped dependency, proving the mediator resolved it from a scope.</summary>
    private sealed class ScopedDependencyHandler(ScopedDependency dependency)
        : IRequestHandler<ScopedDependencyRequest, Result>
    {
        public ValueTask<Result> HandleAsync(
            ScopedDependencyRequest request,
            CancellationToken cancellationToken = default)
        {
            dependency.Touch();
            return ValueTask.FromResult(Result.Success());
        }
    }

    // -----------------------------------------------------------------------
    // Disposable handler (captive-disposal leak)
    // -----------------------------------------------------------------------

    /// <summary>A request handled by a transient <see cref="IDisposable"/> handler.</summary>
    private sealed record DisposableHandlerRequest : IRequest<Result>;

    /// <summary>Tracks how many times the disposable handler instances are disposed.</summary>
    private sealed class DisposalCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Increment() => Interlocked.Increment(ref _count);
    }

    /// <summary>A transient handler that must be disposed when its resolving scope ends.</summary>
    private sealed class DisposableHandler(DisposalCounter counter)
        : IRequestHandler<DisposableHandlerRequest, Result>, IDisposable
    {
        public ValueTask<Result> HandleAsync(
            DisposableHandlerRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Result.Success());

        public void Dispose() => counter.Increment();
    }

    // -----------------------------------------------------------------------
    // Captive-dependency: scoped dependency resolves through a child scope
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that, with <c>ValidateScopes=true</c>, a request whose handler depends on a scoped
    /// service can be sent through a mediator resolved from a CHILD scope.
    /// </summary>
    /// <remarks>
    /// With the mediator registered as a singleton it captures the root provider, and this throws
    /// <see cref="InvalidOperationException"/> ("Cannot resolve scoped service ... from root provider").
    /// A scoped (or transient) mediator resolves the handler from the request scope, where the scoped
    /// dependency is available.
    /// </remarks>
    [Fact]
    public async Task SendAsync_WithScopedHandlerDependency_ResolvesFromRequestScope()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(ScopedDependencyRequest).Assembly);
        _ = services.AddScoped<ScopedDependency>();
        _ = services.AddTransient<IRequestHandler<ScopedDependencyRequest, Result>, ScopedDependencyHandler>();

        // Mirror the ASP.NET Core development default: validate that scoped services are never
        // resolved from the root provider.
        await using var rootProvider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        //---------------------------------------------------------
        // Act — resolve the mediator from a per-request child scope and dispatch.
        //---------------------------------------------------------
        await using var scope = rootProvider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.SendAsync<ScopedDependencyRequest, Result>(new ScopedDependencyRequest());

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(
            result.IsSuccess,
            "SendAsync must resolve the scoped handler dependency from the request scope.");
    }

    // -----------------------------------------------------------------------
    // Captive-disposal: transient IDisposable handlers disposed per scope
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a transient <see cref="IDisposable"/> handler resolved via the mediator is disposed
    /// when its resolving scope is disposed — exactly once per scope, not leaked to the root provider.
    /// </summary>
    /// <remarks>
    /// With a singleton mediator capturing the root provider, the transient handler is tracked by the
    /// root container and is disposed only when the ROOT provider is disposed (a captive-disposal leak):
    /// the disposal count is still <c>0</c> immediately after the scope ends.
    /// </remarks>
    [Fact]
    public async Task SendAsync_WithTransientDisposableHandler_DisposesHandlerWhenScopeEnds()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var counter = new DisposalCounter();

        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(DisposableHandlerRequest).Assembly);
        _ = services.AddSingleton(counter);
        _ = services.AddTransient<IRequestHandler<DisposableHandlerRequest, Result>, DisposableHandler>();

        await using var rootProvider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        //---------------------------------------------------------
        // Act — dispatch inside a scope, then dispose the scope.
        //---------------------------------------------------------
        await using (var scope = rootProvider.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            _ = await mediator.SendAsync<DisposableHandlerRequest, Result>(new DisposableHandlerRequest());
        }

        //---------------------------------------------------------
        // Assert — the transient handler must have been disposed exactly once with the scope.
        //---------------------------------------------------------
        Assert.Equal(1, counter.Count);
    }
}
