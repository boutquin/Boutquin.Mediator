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

/// <summary>
/// Contains unit tests for the <c>WaitWithRetry.WaitWithRetryAsync&lt;TResult&gt;</c> method, which provides functionality  to
/// execute operations with retry logic and optional timeout handling.
/// </summary>
/// <remarks>This test class verifies various scenarios for the retry mechanism, including successful execution on
/// the  first attempt, retries after exceptions, handling of timeout conditions, and behavior when retries are
/// exhausted.</remarks>
public sealed class WaitWithRetryGenericTests
{
    /// <summary>
    /// Verifies that the <c>WaitWithRetry.WaitWithRetryAsync</c> method returns the expected result when the first attempt
    /// succeeds.
    /// </summary>
    /// <remarks>This test ensures that the retry mechanism does not interfere with the result when the
    /// operation completes successfully on the first attempt.</remarks>
    [Fact]
    public async Task Returns_result_when_first_attempt_succeeds()
    {
        // Arrange
        var (sut, _) = WaitWithRetryFixture.Create();
        var settings = new TestWaitSettings();
        const int expected = 42;

        // Act
        var actual = await sut.WaitWithRetryAsync(() => Task.FromResult(expected), settings).ConfigureAwait(true);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Tests that the <c>WaitWithRetryAsync</c> method retries the operation upon encountering an exception and
    /// successfully completes when the operation eventually succeeds.
    /// </summary>
    /// <remarks>This test verifies the retry behavior of <c>WaitWithRetryAsync</c> by simulating a scenario
    /// where the operation fails initially and succeeds on a subsequent attempt. It ensures that the correct number of
    /// retries is performed, the final result is returned, and a warning is logged for the retry.</remarks>
    [Fact]
    public async Task Retries_on_exception_then_succeeds()
    {
        // Arrange
        var (sut, log) = WaitWithRetryFixture.Create();
        var settings = new TestWaitSettings { NumberOfRetries = 2 };
        var attempt = 0;

        // Act
        var result = await sut.WaitWithRetryAsync(() => Task.Run(FailingThenOk), settings).ConfigureAwait(true);

        // Assert
        Assert.Equal(123, result);
        Assert.Equal(2, attempt);                     // one failure + one success
        log.Verify(
            l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);                              // exactly one retry logged
        return;

        Task<int> FailingThenOk()
        {
            if (++attempt < 2)
            {
                throw new InvalidOperationException("bang");
            }

            return Task.FromResult(123);
        }
    }

    /// <summary>
    /// Tests that the <c>WaitWithRetry.WaitWithRetryAsync&lt;T&gt;</c> method throws an <see cref="InvalidOperationException"/> 
    /// after the configured number of retries is exhausted.
    /// </summary>
    /// <remarks>This test verifies the behavior of the retry mechanism when the operation consistently fails.
    /// It ensures that the method correctly propagates the exception after all retry attempts have been made.</remarks>
    [Fact]
    public async Task Throws_after_retries_exhausted()
    {
        // Arrange
        var (sut, _) = WaitWithRetryFixture.Create();
        var settings = new TestWaitSettings { NumberOfRetries = 1 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.WaitWithRetryAsync<int>(() => throw new InvalidOperationException("always bad"), settings)).ConfigureAwait(true);
    }

    /// <summary>
    /// Tests that the operation is aborted when a timeout occurs and the <see
    /// cref="TestWaitSettings.ContinueAfterTimeout"/> property is set to <see langword="false"/>.
    /// </summary>
    /// <remarks>This test verifies that when the <see cref="TestWaitSettings.ContinueAfterTimeout"/> property
    /// is set to <see langword="false"/>,  the operation does not retry after a timeout and throws a <see
    /// cref="TimeoutRejectedException"/>.  It ensures that only one attempt is made, even if multiple retries are
    /// configured.</remarks>
    [Fact]
    public async Task Aborts_on_timeout_when_ContinueAfterTimeout_false()
    {
        // Arrange
        var (sut, _) = WaitWithRetryFixture.Create();
        var settings = new TestWaitSettings
        {
            NumberOfRetries = 3,
            ContinueAfterTimeout = false,
            TimeoutPerAttempt = TimeSpan.FromMilliseconds(50)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            sut.WaitWithRetryAsync(Slow, settings)).ConfigureAwait(true);

        // Only one attempt expected
        Assert.IsType<TimeoutRejectedException>(ex);
        return;

        // Simulate a slow operation that exceeds the timeout
        static async Task<int> Slow() { await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false); return 0; }
    }

    /// <summary>
    /// Tests that the operation is retried the specified number of times when a timeout occurs and  <see
    /// cref="TestWaitSettings.ContinueAfterTimeout"/> is set to <see langword="true"/>.
    /// </summary>
    /// <remarks>This test verifies that the retry mechanism correctly handles timeouts by continuing to retry
    /// the operation until the maximum number of retries is reached. It ensures that the retry count  matches the
    /// expected value and that a <see cref="TimeoutRejectedException"/> is thrown when  the operation ultimately fails
    /// due to exceeding the timeout.</remarks>
    [Fact]
    public async Task Retries_on_timeout_when_ContinueAfterTimeout_true()
    {
        // Arrange
        var (sut, _) = WaitWithRetryFixture.Create();
        var hitCount = 0;

        var settings = new TestWaitSettings
        {
            NumberOfRetries = 2,
            ContinueAfterTimeout = true,
            TimeoutPerAttempt = TimeSpan.FromMilliseconds(50)
        };

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            sut.WaitWithRetryAsync(Slow, settings)).ConfigureAwait(true);

        // one initial + two retries = 3 attempts total
        Assert.Equal(3, hitCount);
        return;

        // Simulate a slow operation that exceeds the timeout
        Task<int> Slow() { hitCount++; return Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => 1, TaskScheduler.Default); }
    }

    /// <summary>
    /// RETRY-CT-DROP-001: an already-cancelled <see cref="CancellationToken"/> passed to
    /// <c>WaitWithRetry.WaitWithRetryAsync&lt;T&gt;</c> must abort the retry loop promptly with an
    /// <see cref="OperationCanceledException"/> — the token must reach Polly's
    /// <c>ExecuteAsync(ct => ..., cancellationToken)</c> so the inter-retry sleeps and attempts
    /// observe cancellation instead of running the full retry budget.
    /// </summary>
    [Fact]
    public async Task Honors_cancellation_token_generic()
    {
        // Arrange
        var (sut, _) = WaitWithRetryFixture.Create();
        var settings = new TestWaitSettings
        {
            NumberOfRetries = 5,
            SleepTimeDurationProvider = _ => TimeSpan.FromSeconds(2) // sleeps only elapse if the token is ignored
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(true);

        var attempts = 0;

        // Act & Assert — must fail with cancellation, not run all attempts / sleeps
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.WaitWithRetryAsync<int>(AlwaysThrows, settings, cts.Token)).ConfigureAwait(true);

        // Token observed before any 30 s sleep elapsed and before the full attempt budget ran
        Assert.True(attempts <= 1, $"Expected the cancelled token to abort before retrying; ran {attempts} attempts.");
        return;

        Task<int> AlwaysThrows()
        {
            attempts++;
            throw new InvalidOperationException("bang");
        }
    }
}
