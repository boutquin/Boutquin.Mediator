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

namespace Boutquin.Reliability;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Polly;
using Polly.Timeout;

/// <summary>
/// Provides functionality to execute asynchronous actions with configurable retry and timeout policies.
/// </summary>
/// <remarks>This class is designed to handle scenarios where an asynchronous operation may fail intermittently
/// due to transient errors or timeouts.  It applies retry and timeout policies to ensure robust execution of the
/// provided actions. The retry policy allows customization of  the number of retries, delay between attempts, and
/// exception handling behavior. The timeout policy ensures that each attempt does not  exceed the specified duration.
/// Logging is performed for each retry attempt, providing insights into the retry process.</remarks>
/// <param name="logger"></param>
public sealed class WaitWithRetry(ILogger<WaitWithRetry> logger) : IWaitWithRetry
{
    // ──────────────────────────────────────────────────────────────────────────
    // 1. Generic overload
    // ──────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Executes the specified asynchronous action with retry and timeout policies.
    /// </summary>
    /// <remarks>This method applies a timeout policy to each attempt and a retry policy for handling
    /// exceptions.  The retry policy can be customized to handle specific exceptions or continue after a timeout, 
    /// based on the provided <paramref name="waitSettings"/>.</remarks>
    /// <typeparam name="T">The type of the result returned by the asynchronous action.</typeparam>
    /// <param name="action">The asynchronous action to execute. Cannot be <see langword="null"/>.</param>
    /// <param name="waitSettings">The settings that define the retry and timeout behavior, including the number of retries,  timeout duration per
    /// attempt, and the delay between retries. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token observed across the retry loop. When cancelled it interrupts the inter-retry
    /// sleep and the in-flight attempt, so a cancelled caller fails promptly with an
    /// <see cref="OperationCanceledException"/> rather than running the remaining retry budget.</param>
    /// <returns>A task that represents the result of the asynchronous action. If the action completes successfully,  the task
    /// contains the result of type <typeparamref name="T"/>. If the action fails after all retries,  the task will
    /// propagate the exception.</returns>
    public Task<T> WaitWithRetryAsync<T>(Func<Task<T>> action, IWaitSettings waitSettings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(waitSettings);

        var timeoutPolicy = Policy.TimeoutAsync<T>(
            waitSettings.TimeoutPerAttempt,
            TimeoutStrategy.Pessimistic);

        var retryPolicy = Policy<T>
            .Handle<Exception>(ex =>
                waitSettings.ContinueAfterTimeout || ex is not TimeoutRejectedException)
            .WaitAndRetryAsync(
                retryCount: waitSettings.NumberOfRetries,
                sleepDurationProvider: attempt => waitSettings.SleepTimeDurationProvider(attempt),
                onRetryAsync: (outcome, delay, attempt, _) =>
                {
                    // outcome.Exception is never null here because we only retry on exceptions
                    var exception = outcome.Exception!;
                    logger.LogWarning(
                        exception,
                        "Retry {Attempt} in {Delay}.",
                        attempt,
                        delay);
                    return Task.CompletedTask;
                });

        return Policy.WrapAsync(retryPolicy, timeoutPolicy)
                     .ExecuteAsync(
                         ct => ct.IsCancellationRequested ? Task.FromCanceled<T>(ct) : action(),
                         cancellationToken);
    }

    /// <summary>
    /// Executes the specified asynchronous action with retry and timeout policies and no cancellation token.
    /// </summary>
    /// <remarks>Equivalent to calling the three-argument overload with <see cref="CancellationToken.None"/>.</remarks>
    /// <typeparam name="T">The type of the result returned by the asynchronous action.</typeparam>
    /// <param name="action">The asynchronous action to execute. Cannot be <see langword="null"/>.</param>
    /// <param name="waitSettings">The retry and timeout settings. Cannot be <see langword="null"/>.</param>
    /// <returns>A task whose result is the action's result, or which propagates the exception once retries are exhausted.</returns>
    public Task<T> WaitWithRetryAsync<T>(Func<Task<T>> action, IWaitSettings waitSettings)
        => WaitWithRetryAsync(action, waitSettings, CancellationToken.None);

    // ──────────────────────────────────────────────────────────────────────────
    // 2. Non‑generic overload
    // ──────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Executes the specified asynchronous action with retry and timeout policies.
    /// </summary>
    /// <remarks>This method applies a retry policy and a timeout policy to the provided action. The retry
    /// policy  handles exceptions based on the <paramref name="waitSettings"/> configuration, including whether  to
    /// continue after a timeout. The timeout policy ensures that each attempt does not exceed the  specified timeout
    /// duration.  If the action fails due to an exception or timeout, the method retries the action according to  the
    /// configured number of retries and delay between attempts. Logging is performed for each retry  attempt, including
    /// details about the exception and delay.</remarks>
    /// <param name="action">The asynchronous action to execute. Cannot be <see langword="null"/>.</param>
    /// <param name="waitSettings">The settings that define the retry and timeout behavior, including the number of retries,  timeout duration per
    /// attempt, and the delay between retries. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token observed across the retry loop. When cancelled it interrupts the inter-retry
    /// sleep and the in-flight attempt, so a cancelled caller fails promptly with an
    /// <see cref="OperationCanceledException"/> rather than running the remaining retry budget.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the action  has been successfully
    /// executed or when the retry and timeout policies are exhausted.</returns>
    public Task WaitWithRetryAsync(Func<Task> action, IWaitSettings waitSettings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(waitSettings);

        var timeoutPolicy = Policy.TimeoutAsync(
            waitSettings.TimeoutPerAttempt,
            TimeoutStrategy.Pessimistic);

        var retryPolicy = Policy
            .Handle<Exception>(ex =>
                waitSettings.ContinueAfterTimeout || ex is not TimeoutRejectedException)
            .WaitAndRetryAsync(
                retryCount: waitSettings.NumberOfRetries,
                sleepDurationProvider: attempt => waitSettings.SleepTimeDurationProvider(attempt),
                onRetryAsync: (exception, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        exception,
                        "Retry {Attempt} in {Delay}.",
                        attempt,
                        delay);
                    return Task.CompletedTask;
                });

        return Policy.WrapAsync(retryPolicy, timeoutPolicy)
                     .ExecuteAsync(
                         ct => ct.IsCancellationRequested ? Task.FromCanceled(ct) : action(),
                         cancellationToken);
    }

    /// <summary>
    /// Executes the specified asynchronous action with retry and timeout policies and no cancellation token.
    /// </summary>
    /// <remarks>Equivalent to calling the three-argument overload with <see cref="CancellationToken.None"/>. It exists
    /// so that only the generic overload carries an optional parameter (RS0026).</remarks>
    /// <param name="action">The asynchronous action to execute. Cannot be <see langword="null"/>.</param>
    /// <param name="waitSettings">The retry and timeout settings. Cannot be <see langword="null"/>.</param>
    /// <returns>A task that completes when the action succeeds or the retry and timeout policies are exhausted.</returns>
    public Task WaitWithRetryAsync(Func<Task> action, IWaitSettings waitSettings)
        => WaitWithRetryAsync(action, waitSettings, CancellationToken.None);
}
