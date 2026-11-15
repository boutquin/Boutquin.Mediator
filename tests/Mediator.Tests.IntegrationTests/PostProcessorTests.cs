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
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Integration tests for <see cref="IRequestPostProcessor{TRequest,TResponse}"/> dispatch behaviour.
/// </summary>
/// <remarks>
/// Covers CR-23 (post-processors run after the request handler completes).
/// </remarks>
public sealed class PostProcessorTests
{
    // -----------------------------------------------------------------------
    // Test request + handler types
    // -----------------------------------------------------------------------

    /// <summary>A simple tracking request used across tests.</summary>
    private sealed record TrackingRequest : IRequest<Result>;

    /// <summary>Handler that appends <c>"handler"</c> to the log and returns success.</summary>
    private sealed class TrackingHandler(List<string> log) : IRequestHandler<TrackingRequest, Result>
    {
        public ValueTask<Result> HandleAsync(TrackingRequest request, CancellationToken cancellationToken = default)
        {
            log.Add("handler");
            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>Post-processor that appends <c>"postprocessor"</c> to the log.</summary>
    private sealed class TrackingPostProcessor(List<string> log) : IRequestPostProcessor<TrackingRequest, Result>
    {
        public ValueTask<Result> ProcessAsync(TrackingRequest request, Result response, CancellationToken cancellationToken = default)
        {
            log.Add("postprocessor");
            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>A second post-processor that appends <c>"postprocessor2"</c> to the log.</summary>
    private sealed class TrackingPostProcessor2(List<string> log) : IRequestPostProcessor<TrackingRequest, Result>
    {
        public ValueTask<Result> ProcessAsync(TrackingRequest request, Result response, CancellationToken cancellationToken = default)
        {
            log.Add("postprocessor2");
            return ValueTask.FromResult(Result.Success());
        }
    }

    // -----------------------------------------------------------------------
    // CR-23-T1: Post-processor runs after handler
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a registered post-processor executes after the request handler and sees the handler's response.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithPostProcessor_RunsHandlerThenPostProcessor()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var log = new List<string>();

        var services = new ServiceCollection();
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();
        _ = services.AddTransient<IRequestHandler<TrackingRequest, Result>>(_ => new TrackingHandler(log));
        _ = services.AddTransient<IRequestPostProcessor<TrackingRequest, Result>>(_ => new TrackingPostProcessor(log));

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.SendAsync<TrackingRequest, Result>(new TrackingRequest()).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess, "SendAsync must succeed.");
        Assert.Equal(2, log.Count);
        Assert.Equal("handler", log[0]);
        Assert.Equal("postprocessor", log[1]);
    }

    /// <summary>Post-processor that returns a failure result.</summary>
    private sealed class FailingPostProcessor : IRequestPostProcessor<TrackingRequest, Result>
    {
        public static readonly Error PostProcessorError = Error.InternalServerError("PostProcessor.Fail", "Post-processor failed");

        public ValueTask<Result> ProcessAsync(TrackingRequest request, Result response, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Result.Failure(PostProcessorError));
    }

    // -----------------------------------------------------------------------
    // S9-04: Post-processor failure propagates to caller
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when a post-processor returns a failure, that failure is propagated
    /// back to the caller instead of the handler's success response.
    /// </summary>
    [Fact]
    public async Task SendAsync_PostProcessorReturnsFailure_CallerReceivesFailure()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var log = new List<string>();

        var services = new ServiceCollection();
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();
        _ = services.AddTransient<IRequestHandler<TrackingRequest, Result>>(_ => new TrackingHandler(log));
        _ = services.AddTransient<IRequestPostProcessor<TrackingRequest, Result>, FailingPostProcessor>();

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.SendAsync<TrackingRequest, Result>(new TrackingRequest()).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.False(result.IsSuccess, "Caller must receive a failure when a post-processor fails.");
        Assert.Equal(FailingPostProcessor.PostProcessorError.Code, result.Error.Code);
        Assert.Contains("handler", log); // Handler ran before the post-processor
    }

    // -----------------------------------------------------------------------
    // CR-23-T2: Multiple post-processors all run (fan-out)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when two post-processors are registered, both run after the handler completes.
    /// </summary>
    [Fact]
    public async Task SendAsync_WithTwoPostProcessors_BothRunAfterHandler()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var log = new List<string>();

        var services = new ServiceCollection();
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();
        _ = services.AddTransient<IRequestHandler<TrackingRequest, Result>>(_ => new TrackingHandler(log));
        _ = services.AddTransient<IRequestPostProcessor<TrackingRequest, Result>>(_ => new TrackingPostProcessor(log));
        _ = services.AddTransient<IRequestPostProcessor<TrackingRequest, Result>>(_ => new TrackingPostProcessor2(log));

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.SendAsync<TrackingRequest, Result>(new TrackingRequest()).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess, "SendAsync must succeed.");
        Assert.Equal(3, log.Count);
        Assert.Equal("handler", log[0]);
        // Both post-processors run; order matches DI registration order
        Assert.Contains("postprocessor", log);
        Assert.Contains("postprocessor2", log);
    }
}
