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

using Boutquin.Mediator.Reliability;

/// <summary>
/// Contains unit tests for verifying the behavior of the <c>WaitWithRetry.WaitWithRetryAsync</c> method.
/// </summary>
/// <remarks>This class includes tests to ensure that the retry mechanism, timeout handling, and sleep provider
/// functionality of the <c>WaitWithRetry.WaitWithRetryAsync</c> method work as expected under various conditions.</remarks>
public sealed class WaitWithRetryTests
{
    /// <summary>
    /// Tests that a method retries a failing operation the specified number of times and completes successfully.
    /// </summary>
    /// <remarks>This test verifies that the retry mechanism correctly handles transient failures by retrying
    /// the operation until it succeeds, as defined by the provided retry settings. The operation being tested simulates
    /// a task that fails once before succeeding.</remarks>
    [Fact]
    public async Task Retries_then_completes_void_successfully()
    {
        // Arrange
        var (sut, _) = WaitWithRetryFixture.Create();
        var settings = new TestWaitSettings { NumberOfRetries = 2 };

        var attempt = 0;

        // Act
        await sut.WaitWithRetryAsync(() => Task.Run(SometimesFails), settings).ConfigureAwait(true);

        // Assert
        Assert.Equal(2, attempt);
        return;

        // This method simulates a task that fails once before succeeding.
        Task SometimesFails()
        {
            if (++attempt < 2)
            {
                throw new InvalidOperationException("fail");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Verifies that the sleep duration provider is invoked for each retry attempt during a failed operation.
    /// </summary>
    /// <remarks>This test ensures that the sleep duration provider specified in the retry settings is called
    /// exactly      the number of times corresponding to the retry attempts, excluding the initial attempt. It
    /// simulates      a failure scenario where the operation always throws an exception, triggering retries.</remarks>
    [Fact]
    public async Task Should_call_sleep_provider_each_retry()
    {
        // Arrange
        var providerHits = 0;
        var settings = new TestWaitSettings
        {
            NumberOfRetries = 2,
            SleepTimeDurationProvider = _ =>
            {
                providerHits++;
                return TimeSpan.Zero;
            }
        };

        var (sut, _) = WaitWithRetryFixture.Create();

        var attempts = 0;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.WaitWithRetryAsync(() => Task.Run(AlwaysThrows), settings)).ConfigureAwait(true);

        // provider is invoked exactly for the RETRIES (not the first attempt)
        Assert.Equal(settings.NumberOfRetries, providerHits);
        Assert.Equal(settings.NumberOfRetries + 1, attempts);
        return;

        // This method always throws an exception to simulate a failure.
        Task AlwaysThrows()
        {
            attempts++;
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Tests that the <c>WaitWithRetry.WaitWithRetryAsync</c> method throws a <see cref="TimeoutRejectedException"/>  when a
    /// timeout occurs during the execution of a void-returning task.
    /// </summary>
    /// <remarks>This test verifies the behavior of the <c>WaitWithRetry.WaitWithRetryAsync</c> method when the
    /// provided  task exceeds the configured timeout duration. The test uses a simulated slow task to ensure  the
    /// timeout condition is met.</remarks>
    [Fact]
    public async Task Aborts_on_timeout_with_void_method()
    {
        // Arrange
        var (sut, _) = WaitWithRetryFixture.Create();
        var settings = new TestWaitSettings
        {
            TimeoutPerAttempt = TimeSpan.FromMilliseconds(50),
            ContinueAfterTimeout = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            sut.WaitWithRetryAsync(Slow, settings)).ConfigureAwait(true);
        return;

        // This method simulates a slow task that should time out.
        static Task Slow() => Task.Delay(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// RETRY-CT-DROP-001: an already-cancelled <see cref="CancellationToken"/> passed to the
    /// non-generic <c>WaitWithRetry.WaitWithRetryAsync</c> must abort the retry loop promptly
    /// with an <see cref="OperationCanceledException"/> rather than running every retry and sleep.
    /// </summary>
    [Fact]
    public async Task Honors_cancellation_token_void()
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

        // Act & Assert — cancellation must short-circuit the loop
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sut.WaitWithRetryAsync(AlwaysThrows, settings, cts.Token)).ConfigureAwait(true);

        Assert.True(attempts <= 1, $"Expected the cancelled token to abort before retrying; ran {attempts} attempts.");
        return;

        Task AlwaysThrows()
        {
            attempts++;
            throw new InvalidOperationException("bang");
        }
    }

    /// <summary>
    /// RETRY-EMPTYSLEEP-001 (F-V2-01): <see cref="RetrySettings.SleepTimeDurationProvider"/> must be
    /// defensive when <see cref="RetrySettings.SleepDurationsMs"/> is an EMPTY (non-null) list — it
    /// must fall back to the default duration instead of indexing the empty array. A consumer of the
    /// public <c>RetrySettings</c> / <c>IWaitWithRetry</c> surface who sets <c>SleepDurationsMs = []</c>
    /// otherwise triggers <see cref="ArgumentOutOfRangeException"/> ("Index was out of range") on the
    /// first retry.
    /// </summary>
    [Fact]
    public void SleepTimeDurationProvider_WithEmptyList_FallsBackToDefaultDuration()
    {
        // Arrange — an empty (non-null) sleep-duration list, reachable via the public RetrySettings API
        var settings = new RetrySettings { SleepDurationsMs = [] };

        // Act — provider invoked with Polly's 1-based first-retry attempt index
        var sleep = settings.SleepTimeDurationProvider(1);

        // Assert — must coalesce to the 100 ms default rather than indexing the empty list
        Assert.Equal(TimeSpan.FromMilliseconds(100), sleep);
    }
}
