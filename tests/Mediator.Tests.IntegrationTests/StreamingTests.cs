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
/// Integration tests for <see cref="IStreamer.CreateStream{TRequest,TResponse}"/> dispatch behaviour.
/// </summary>
/// <remarks>
/// Covers CR-22 (streaming end-to-end via <see cref="IStreamRequestHandler{TRequest,TResponse}"/>).
/// </remarks>
public sealed class StreamingTests
{
    // -----------------------------------------------------------------------
    // Test stream request types
    // -----------------------------------------------------------------------

    /// <summary>A stream request that asks for <paramref name="Count"/> integers.</summary>
    private sealed record NumberStreamRequest(int Count) : IStreamRequest<int>;

    /// <summary>Handler that yields 1..Count as successful <see cref="Result{TValue}"/> items.</summary>
    private sealed class NumberStreamHandler : IStreamRequestHandler<NumberStreamRequest, int>
    {
        public async IAsyncEnumerable<Result<int>> HandleAsync(
            NumberStreamRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 1; i <= request.Count; i++)
            {
                await Task.Yield(); // simulate async work
                yield return Result.Success(i);
            }
        }
    }

    // -----------------------------------------------------------------------
    // CR-22-T1: Multi-item stream end-to-end
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="IStreamer.CreateStream{TRequest,TResponse}"/> dispatches to the registered
    /// handler and the caller receives all yielded items.
    /// </summary>
    [Fact]
    public async Task CreateStream_WithThreeItemHandler_ReturnsAllThreeItems()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();
        _ = services.AddTransient<IStreamRequestHandler<NumberStreamRequest, int>, NumberStreamHandler>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var request = new NumberStreamRequest(3);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var results = new List<int>();
        await foreach (var item in mediator.CreateStream<NumberStreamRequest, int>(request)
                           .ConfigureAwait(false))
        {
            Assert.True(item.IsSuccess, "Each streamed item must be a success.");
            results.Add(item.Value);
        }

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.Equal(3, results.Count);
        Assert.Equal([1, 2, 3], results);
    }

    // -----------------------------------------------------------------------
    // S9-05: IStreamPipelineBehavior wraps the handler's output
    // -----------------------------------------------------------------------

    /// <summary>
    /// A stream pipeline behavior that doubles each successful item's value.
    /// </summary>
    private sealed class DoublingStreamBehavior : IStreamPipelineBehavior<NumberStreamRequest, int>
    {
        public async IAsyncEnumerable<Result<int>> HandleAsync(
            NumberStreamRequest request,
            StreamHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in next().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return item.IsSuccess
                    ? Result.Success(item.Value * 2)
                    : item;
            }
        }
    }

    /// <summary>
    /// Verifies that a registered <see cref="IStreamPipelineBehavior{TRequest,TResponse}"/> wraps
    /// the handler's output and transforms each item in the stream.
    /// </summary>
    [Fact]
    public async Task CreateStream_WithStreamPipelineBehavior_TransformsItems()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();
        _ = services.AddTransient<IStreamRequestHandler<NumberStreamRequest, int>, NumberStreamHandler>();
        _ = services.AddTransient<IStreamPipelineBehavior<NumberStreamRequest, int>, DoublingStreamBehavior>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var request = new NumberStreamRequest(3);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var results = new List<int>();
        await foreach (var item in mediator.CreateStream<NumberStreamRequest, int>(request)
                           .ConfigureAwait(false))
        {
            Assert.True(item.IsSuccess);
            results.Add(item.Value);
        }

        //---------------------------------------------------------
        // Assert — behavior doubled each value
        //---------------------------------------------------------
        Assert.Equal(3, results.Count);
        Assert.Equal([2, 4, 6], results);
    }

    // -----------------------------------------------------------------------
    // CR-22-T2: Empty stream
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a handler that yields no items results in an empty sequence with no exceptions.
    /// </summary>
    [Fact]
    public async Task CreateStream_WithEmptyHandler_YieldsNoItems()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();
        _ = services.AddTransient<IStreamRequestHandler<NumberStreamRequest, int>, NumberStreamHandler>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var request = new NumberStreamRequest(0); // count=0 means nothing yielded

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var itemCount = 0;
        await foreach (var _ in mediator.CreateStream<NumberStreamRequest, int>(request)
                           .ConfigureAwait(false))
        {
            itemCount++;
        }

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.Equal(0, itemCount);
    }
}
