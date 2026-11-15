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

namespace Boutquin.Tests.UnitTests;

// Boutquin
using Boutquin.Domain.Abstractions;
using Boutquin.Mediator.Abstractions;
using Boutquin.Mediator.Reliability;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

/// <summary>
/// Contains unit tests for verifying the behavior of the <see cref="RetryBehavior{TRequest, TResult}"/> class.
/// </summary>
/// <remarks>This test class ensures that the <see cref="RetryBehavior{TRequest, TResult}"/> implementation
/// behaves correctly under various conditions, including scenarios where retry is enabled or disabled, null arguments
/// are provided,  and exceptions are propagated. The tests validate the interaction between the retry mechanism,
/// configuration settings,  and the next delegate in the pipeline.</remarks>
public class RetryBehaviorTests
{
    /// <summary>
    /// Represents a request for a dummy operation that produces a <see cref="Result"/>.
    /// </summary>
    /// <remarks>This class is used as part of the request-response pattern in operations that return a <see
    /// cref="Result"/>. It implements the <see cref="IRequest{TResponse}"/> interface, where <c>TResponse</c> is
    /// <see cref="Result"/>.</remarks>
    internal sealed class DummyRequest : IRequest<Result>;

    /// <summary>
    /// Tests the behavior of the <see cref="RetryBehavior{TRequest, TResponse}"/> when retry is disabled. Ensures that
    /// the next handler in the pipeline is called directly without invoking retry logic.
    /// </summary>
    /// <remarks>This test verifies that the <see cref="RetryBehavior{TRequest, TResponse}"/> correctly
    /// bypasses retry logic when the retry settings are disabled in the configuration. It ensures that the next handler
    /// is executed and that no calls are made to the retry mechanism.</remarks>
    [Fact]
    public async Task HandleAsync_RetryDisabled_CallsNextDirectly()
    {
        // Arrange
        var waitWithRetry = new Mock<IWaitWithRetry>(MockBehavior.Strict);
        // Setup mock to throw if WaitWithRetryAsync is called (should not be called when retry is disabled)
        waitWithRetry
            .Setup(w => w.WaitWithRetryAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<IWaitSettings>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("WaitWithRetryAsync should not be called when retry is disabled."));
        var inMemorySettings = new Dictionary<string, string>
        {
            // Use the key format expected by your code, e.g.:
            // "MediatorSettings:{RequestTypeFullName}:RetrySettings:Enabled"
            ["MediatorSettings:Boutquin.Tests.UnitTests.RetryBehaviorTests+DummyRequest:RetrySettings:Enabled"] = "false"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        var logger = new Mock<ILogger<DummyRequest>>();
        var behavior = new RetryBehavior<DummyRequest, Result>(waitWithRetry.Object, config, logger.Object);
        var called = false;
        var request = new DummyRequest();
        var response = Result.Success();

        // Act
        var result = await behavior.HandleAsync(request, Next).ConfigureAwait(true);

        // Assert
        Assert.True(called);
        Assert.Same(response, result);
        waitWithRetry.VerifyNoOtherCalls();
        logger.Verify(l => l.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("RetryBehavior is disabled")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        return;

        // The Next delegate is set up to return a dummy result
        ValueTask<Result> Next() { called = true; return ValueTask.FromResult(response); }
    }

    /// <summary>
    /// Tests the <see cref="RetryBehavior{TRequest, TResult}.HandleAsync"/> method to ensure that when retry is
    /// enabled,  the <c>IWaitWithRetry.WaitWithRetryAsync</c> mechanism is invoked as expected.
    /// </summary>
    /// <remarks>This test verifies that the retry behavior correctly applies the retry logic when the
    /// configuration specifies that  retry is enabled. It ensures that the 
    /// <c>IWaitWithRetry.WaitWithRetryAsync</c> method is called once and that  the expected logging
    /// occurs.</remarks>
    [Fact]
    public async Task HandleAsync_RetryEnabled_UsesWaitWithRetry()
    {
        // Arrange
        var waitWithRetry = new Mock<IWaitWithRetry>();
        var inMemorySettings = new Dictionary<string, string>
        {
            // Use the key format expected by your code, e.g.:
            // "MediatorSettings:{RequestTypeFullName}:RetrySettings:Enabled"
            ["MediatorSettings:Boutquin.Tests.UnitTests.RetryBehaviorTests+DummyRequest:RetrySettings:Enabled"] = "true"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        var logger = new Mock<ILogger<DummyRequest>>();
        var behavior = new RetryBehavior<DummyRequest, Result>(waitWithRetry.Object, config, logger.Object);
        var request = new DummyRequest();
        var response = Result.Success();
        // Setup WaitWithRetryAsync to call the lambda and return its result as Task
        waitWithRetry.Setup(w => w.WaitWithRetryAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<IWaitSettings>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Result>>, IWaitSettings, CancellationToken>(async (func, _, _) => await func().ConfigureAwait(false));

        // Act
        var result = await behavior.HandleAsync(request, Next).ConfigureAwait(true);

        // Assert
        Assert.Same(response, result);
        waitWithRetry.Verify(w => w.WaitWithRetryAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<IWaitSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        logger.Verify(l => l.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Applying RetryBehavior")),
            null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        return;

        // The Next delegate is set up to return a dummy result
        ValueTask<Result> Next() => ValueTask.FromResult(response);
    }

    /// <summary>
    /// Tests the <see cref="RetryBehavior{TRequest, TResult}.HandleAsync"/>
    /// method to ensure it throws an <see cref="ArgumentNullException"/>
    /// when either the request parameter or the next delegate is <see langword="null"/>.
    /// </summary>
    /// <remarks>This test verifies the precondition checks in the
    /// <see cref="RetryBehavior{TRequest, TResult}.HandleAsync"/> method, ensuring that invalid arguments are properly handled by throwing the expected
    /// exception.</remarks>
    [Fact]
    public async Task HandleAsync_ThrowsOnNullRequestOrNext()
    {
        // Arrange
        var waitWithRetry = new Mock<IWaitWithRetry>();
        var inMemorySettings = new Dictionary<string, string>
        {
            // Use the key format expected by your code, e.g.:
            // "MediatorSettings:{RequestTypeFullName}:RetrySettings:Enabled"
            ["MediatorSettings:Boutquin.Tests.UnitTests.RetryBehaviorTests+DummyRequest:RetrySettings:Enabled"] = "true"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        var logger = new Mock<ILogger<DummyRequest>>();
        var behavior = new RetryBehavior<DummyRequest, Result>(waitWithRetry.Object, config, logger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => behavior.HandleAsync(null!, Next).AsTask()).ConfigureAwait(true);
        await Assert.ThrowsAsync<ArgumentNullException>(() => behavior.HandleAsync(new DummyRequest(), null!).AsTask()).ConfigureAwait(true);
        return;

        // The Next delegate is set up to return a dummy result
        static ValueTask<Result> Next() => ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// Tests that the <see cref="RetryBehavior{TRequest, TResult}.HandleAsync"/> method propagates exceptions  thrown
    /// by the next delegate in the pipeline.
    /// </summary>
    /// <remarks>This test verifies that when the next delegate in the pipeline throws an exception, the
    /// exception is  correctly propagated by the <see cref="RetryBehavior{TRequest, TResult}.HandleAsync"/> method. It
    /// ensures  that the retry behavior does not suppress or alter the exception.</remarks>
    [Fact]
    public async Task HandleAsync_PropagatesExceptionFromNext()
    {
        // Arrange
        var waitWithRetry = new Mock<IWaitWithRetry>(MockBehavior.Strict);
        // Setup mock to invoke the delegate and propagate exception
        waitWithRetry
            .Setup(w => w.WaitWithRetryAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<IWaitSettings>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Result>>, IWaitSettings, CancellationToken>((action, _, _) => action());

        var inMemorySettings = new Dictionary<string, string>
        {
            // Use the key format expected by your code, e.g.:
            // "MediatorSettings:{RequestTypeFullName}:RetrySettings:Enabled"
            ["MediatorSettings:Boutquin.Tests.UnitTests.RetryBehaviorTests+DummyRequest:RetrySettings:Enabled"] = "true"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        var logger = new Mock<ILogger<DummyRequest>>();
        var behavior = new RetryBehavior<DummyRequest, Result>(waitWithRetry.Object, config, logger.Object);
        var ex = new InvalidOperationException("fail");

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.HandleAsync(new DummyRequest(), Next).AsTask()).ConfigureAwait(true);
        Assert.Same(ex, thrown);
        return;

        // The Next delegate is set up to throw an exception
        ValueTask<Result> Next() => throw ex;
    }

    /// <summary>
    /// RETRY-BINDERAPPEND-001 (F-V1-01): when a request configures an explicit
    /// <see cref="RetrySettings.SleepDurationsMs"/> array such as <c>[5, 7]</c>, the settings that
    /// <see cref="RetryBehavior{TRequest, TResponse}"/> hands to <c>WaitWithRetryAsync</c> must carry
    /// EXACTLY <c>[5, 7]</c> — not <c>[100, 5, 7]</c>. The .NET configuration binder APPENDS to a
    /// pre-seeded non-null list, so the behavior must not pre-seed the default before <c>Bind()</c>;
    /// otherwise the spurious <c>100</c> is prepended and the consumer's backoff schedule shifts by one slot.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ExplicitSleepDurations_AreNotAppendedToDefault()
    {
        // Arrange — config supplies an explicit [5, 7] backoff schedule for the request type
        var waitWithRetry = new Mock<IWaitWithRetry>();
        IWaitSettings? captured = null;
        waitWithRetry
            .Setup(w => w.WaitWithRetryAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<IWaitSettings>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Result>>, IWaitSettings, CancellationToken>(async (func, settings, _) =>
            {
                captured = settings;
                return await func().ConfigureAwait(false);
            });

        const string prefix = "MediatorSettings:Boutquin.Tests.UnitTests.RetryBehaviorTests+DummyRequest:RetrySettings";
        var inMemorySettings = new Dictionary<string, string>
        {
            [$"{prefix}:Enabled"] = "true",
            [$"{prefix}:SleepDurationsMs:0"] = "5",
            [$"{prefix}:SleepDurationsMs:1"] = "7"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        var logger = new Mock<ILogger<DummyRequest>>();
        var behavior = new RetryBehavior<DummyRequest, Result>(waitWithRetry.Object, config, logger.Object);
        var response = Result.Success();

        // Act
        _ = await behavior.HandleAsync(new DummyRequest(), Next).ConfigureAwait(true);

        // Assert — the captured settings must carry exactly [5, 7], NOT [100, 5, 7]
        var retrySettings = Assert.IsType<RetrySettings>(captured);
        Assert.Equal(new[] { 5, 7 }, retrySettings.SleepDurationsMs);
        return;

        ValueTask<Result> Next() => ValueTask.FromResult(response);
    }

    /// <summary>
    /// RETRY-BINDERAPPEND-001 (F-V1-01): when a request configures NO
    /// <see cref="RetrySettings.SleepDurationsMs"/>, the behavior must still supply the
    /// <c>[100]</c> default — the post-bind null/empty fallback must not regress the default schedule.
    /// </summary>
    [Fact]
    public async Task HandleAsync_NoSleepDurations_AppliesDefaultSchedule()
    {
        // Arrange — config enables retry but supplies no SleepDurationsMs
        var waitWithRetry = new Mock<IWaitWithRetry>();
        IWaitSettings? captured = null;
        waitWithRetry
            .Setup(w => w.WaitWithRetryAsync(It.IsAny<Func<Task<Result>>>(), It.IsAny<IWaitSettings>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task<Result>>, IWaitSettings, CancellationToken>(async (func, settings, _) =>
            {
                captured = settings;
                return await func().ConfigureAwait(false);
            });

        var inMemorySettings = new Dictionary<string, string>
        {
            ["MediatorSettings:Boutquin.Tests.UnitTests.RetryBehaviorTests+DummyRequest:RetrySettings:Enabled"] = "true"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        var logger = new Mock<ILogger<DummyRequest>>();
        var behavior = new RetryBehavior<DummyRequest, Result>(waitWithRetry.Object, config, logger.Object);
        var response = Result.Success();

        // Act
        _ = await behavior.HandleAsync(new DummyRequest(), Next).ConfigureAwait(true);

        // Assert — the default [100] schedule must be present
        var retrySettings = Assert.IsType<RetrySettings>(captured);
        Assert.Equal(new[] { 100 }, retrySettings.SleepDurationsMs);
        return;

        ValueTask<Result> Next() => ValueTask.FromResult(response);
    }

    // ------------------------------------------------------------------
    // S3-02: Constructor null guards
    // ------------------------------------------------------------------

    /// <summary>Verifies that a null <c>waitWithRetry</c> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Constructor_NullWaitWithRetry_ThrowsArgumentNullException()
    {
        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger<DummyRequest>>();

        var ex = Assert.Throws<ArgumentNullException>(
            () => new RetryBehavior<DummyRequest, Result>(null!, config, logger.Object));
        Assert.Equal("waitWithRetry", ex.ParamName);
    }

    /// <summary>Verifies that a null <c>configuration</c> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        var waitWithRetry = new Mock<IWaitWithRetry>();
        var logger = new Mock<ILogger<DummyRequest>>();

        var ex = Assert.Throws<ArgumentNullException>(
            () => new RetryBehavior<DummyRequest, Result>(waitWithRetry.Object, null!, logger.Object));
        Assert.Equal("configuration", ex.ParamName);
    }

    /// <summary>Verifies that a null <c>logger</c> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var waitWithRetry = new Mock<IWaitWithRetry>();
        var config = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<ArgumentNullException>(
            () => new RetryBehavior<DummyRequest, Result>(waitWithRetry.Object, config, null!));
        Assert.Equal("logger", ex.ParamName);
    }
}
