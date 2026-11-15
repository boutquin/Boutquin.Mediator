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

using System.Runtime.CompilerServices;
// Boutquin.Mediator
using Abstractions;
using Boutquin.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests pinning pipeline-behavior <em>execution order</em>: the first-registered behavior
/// must run OUTERMOST (its pre-step first and its post-step last), wrapping every later-registered
/// behavior.
/// </summary>
/// <remarks>
/// This is the regression guard for the load-bearing <c>.Reverse()</c> in
/// <see cref="Helpers.Mediator.SendAsync{TRequest,TResponse}"/> and
/// <see cref="Helpers.Mediator.CreateStream{TRequest,TResponse}"/>. With only a single behavior the
/// invariant is unobservable (one wrapper either way), so removing the reversal would pass the rest of
/// the suite while silently inverting multi-behavior order. These tests register TWO distinct behaviors
/// and assert the exact interleave, so they go RED if the reversal is deleted.
/// </remarks>
public sealed class PipelineBehaviorOrderingTests
{
    // -----------------------------------------------------------------------
    // Request / handler that record into a shared execution trace
    // -----------------------------------------------------------------------

    /// <summary>A request carrying the shared trace that behaviors and the handler append to.</summary>
    private sealed record TracedRequest(List<string> Trace) : IRequest<Result>;

    /// <summary>Handler that records its own execution between the behaviors' before/after marks.</summary>
    private sealed class TracedHandler : IRequestHandler<TracedRequest, Result>
    {
        public ValueTask<Result> HandleAsync(TracedRequest request, CancellationToken cancellationToken = default)
        {
            request.Trace.Add("handler");
            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>A pipeline behavior that appends "{Name}:before" then "{Name}:after" around <c>next()</c>.</summary>
    private abstract class TracingBehavior(string name) : IPipelineBehavior<TracedRequest, Result>
    {
        public async ValueTask<Result> HandleAsync(
            TracedRequest request,
            Func<ValueTask<Result>> next,
            CancellationToken cancellationToken = default)
        {
            request.Trace.Add($"{name}:before");
            var result = await next().ConfigureAwait(false);
            request.Trace.Add($"{name}:after");
            return result;
        }
    }

    private sealed class FirstBehavior() : TracingBehavior("first");

    private sealed class SecondBehavior() : TracingBehavior("second");

    // -----------------------------------------------------------------------
    // Stream variants
    // -----------------------------------------------------------------------

    /// <summary>A stream request carrying the shared trace.</summary>
    private sealed record TracedStreamRequest(List<string> Trace) : IStreamRequest<int>;

    /// <summary>Stream handler that records its execution and yields a single item.</summary>
    private sealed class TracedStreamHandler : IStreamRequestHandler<TracedStreamRequest, int>
    {
        public async IAsyncEnumerable<Result<int>> HandleAsync(
            TracedStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            request.Trace.Add("handler");
            await Task.Yield();
            yield return Result.Success(1);
        }
    }

    /// <summary>A stream pipeline behavior that records before/after around the inner stream.</summary>
    private abstract class TracingStreamBehavior(string name) : IStreamPipelineBehavior<TracedStreamRequest, int>
    {
        public async IAsyncEnumerable<Result<int>> HandleAsync(
            TracedStreamRequest request,
            StreamHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            request.Trace.Add($"{name}:before");
            await foreach (var item in next().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }

            request.Trace.Add($"{name}:after");
        }
    }

    private sealed class FirstStreamBehavior() : TracingStreamBehavior("first");

    private sealed class SecondStreamBehavior() : TracingStreamBehavior("second");

    // -----------------------------------------------------------------------
    // SendAsync ordering
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that with two registered <see cref="IPipelineBehavior{TRequest,TResponse}"/>, the
    /// first-registered behavior wraps the second (first runs outermost), producing the canonical
    /// nested interleave.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithTwoBehaviors_FirstRegisteredRunsOutermost()
    {
        //---------------------------------------------------------
        // Arrange — register FirstBehavior before SecondBehavior.
        //---------------------------------------------------------
        var trace = new List<string>();

        var services = new ServiceCollection();
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();
        _ = services.AddTransient<IRequestHandler<TracedRequest, Result>, TracedHandler>();
        _ = services.AddTransient<IPipelineBehavior<TracedRequest, Result>, FirstBehavior>();
        _ = services.AddTransient<IPipelineBehavior<TracedRequest, Result>, SecondBehavior>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        _ = await mediator.SendAsync<TracedRequest, Result>(new TracedRequest(trace));

        //---------------------------------------------------------
        // Assert — first-registered outermost: first:before, second:before, handler, second:after, first:after.
        //---------------------------------------------------------
        Assert.Equal(
            ["first:before", "second:before", "handler", "second:after", "first:after"],
            trace);
    }

    // -----------------------------------------------------------------------
    // CreateStream ordering
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that with two registered <see cref="IStreamPipelineBehavior{TRequest,TResponse}"/>, the
    /// first-registered behavior wraps the second (first runs outermost).
    /// </summary>
    [Fact]
    public async Task CreateStream_WithTwoBehaviors_FirstRegisteredRunsOutermost()
    {
        //---------------------------------------------------------
        // Arrange — register FirstStreamBehavior before SecondStreamBehavior.
        //---------------------------------------------------------
        var trace = new List<string>();

        var services = new ServiceCollection();
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();
        _ = services.AddTransient<IStreamRequestHandler<TracedStreamRequest, int>, TracedStreamHandler>();
        _ = services.AddTransient<IStreamPipelineBehavior<TracedStreamRequest, int>, FirstStreamBehavior>();
        _ = services.AddTransient<IStreamPipelineBehavior<TracedStreamRequest, int>, SecondStreamBehavior>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act — fully enumerate the stream so both before and after marks are recorded.
        //---------------------------------------------------------
        await foreach (var _ in mediator
                           .CreateStream<TracedStreamRequest, int>(new TracedStreamRequest(trace))
                           .ConfigureAwait(true))
        {
            // drain
        }

        //---------------------------------------------------------
        // Assert — first-registered outermost.
        //---------------------------------------------------------
        Assert.Equal(
            ["first:before", "second:before", "handler", "second:after", "first:after"],
            trace);
    }
}
