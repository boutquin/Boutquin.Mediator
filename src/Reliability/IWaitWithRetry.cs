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

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Defines methods for executing operations with retry logic, including support for both synchronous and asynchronous
/// operations.
/// </summary>
/// <remarks>This interface provides functionality to execute operations with configurable retry behavior,
/// including the ability to specify the number of retries, the delay between attempts, and other retry-related
/// settings. It supports both synchronous and asynchronous operations, ensuring flexibility for various use cases.
/// Callers should ensure that the operations being retried are idempotent to avoid unintended side effects.</remarks>
public interface IWaitWithRetry
{
    /// <summary>
    /// Executes the specified asynchronous action with retry logic, waiting between retries as configured.
    /// </summary>
    /// <remarks>Each attempt runs under <see cref="IWaitSettings.TimeoutPerAttempt"/>. An attempt that throws is
    /// retried up to <see cref="IWaitSettings.NumberOfRetries"/> times, sleeping for
    /// <see cref="IWaitSettings.SleepTimeDurationProvider"/> between attempts; a per-attempt timeout is retried only when
    /// <see cref="IWaitSettings.ContinueAfterTimeout"/> is <see langword="true"/>. Only exceptions trigger a retry — a
    /// returned value, including a failed <c>Result</c>, completes the call. The caller should ensure that the action is
    /// idempotent.</remarks>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">A function that represents the asynchronous action to execute.</param>
    /// <param name="waitSettings">The settings that define the retry behavior, including the wait duration and maximum retry attempts.</param>
    /// <param name="cancellationToken">A token that, when cancelled, aborts the retry loop — interrupting the inter-retry
    /// sleep and the in-flight attempt — so a cancelled request fails promptly with an
    /// <see cref="OperationCanceledException"/> instead of running the remaining retry budget.</param>
    /// <returns>A task whose result is the value returned by the first attempt that does not throw. When every attempt
    /// throws, the task faults with the last attempt's exception.</returns>
    Task<T> WaitWithRetryAsync<T>(
        Func<Task<T>> action,
        IWaitSettings waitSettings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the specified asynchronous action with retry logic and no cancellation token.
    /// </summary>
    /// <remarks>Equivalent to calling the three-argument overload with <see cref="CancellationToken.None"/>. The
    /// overloads are explicit rather than using an optional parameter so the public surface satisfies RS0026/RS0027.</remarks>
    /// <typeparam name="T">The type of the result returned by the action.</typeparam>
    /// <param name="action">A function that represents the asynchronous action to execute.</param>
    /// <param name="waitSettings">The settings that define the retry behavior.</param>
    /// <returns>A task whose result is the outcome of the action after retries.</returns>
    Task<T> WaitWithRetryAsync<T>(
        Func<Task<T>> action,
        IWaitSettings waitSettings);

    /// <summary>
    /// Executes the specified asynchronous action with retry logic, waiting between retries as configured.
    /// </summary>
    /// <remarks>Same retry and timeout semantics as <see cref="WaitWithRetryAsync{T}(Func{Task{T}}, IWaitSettings, CancellationToken)"/>:
    /// an attempt that throws is retried according to <paramref name="waitSettings"/>, and when every attempt throws the
    /// returned task faults with the last attempt's exception.</remarks>
    /// <param name="action">The asynchronous action to execute. This action will be retried upon failure.</param>
    /// <param name="waitSettings">The settings that define the retry behavior, including the number of retries and the delay between attempts.</param>
    /// <param name="cancellationToken">A token that, when cancelled, aborts the retry loop — interrupting the inter-retry
    /// sleep and the in-flight attempt — so a cancelled request fails promptly with an
    /// <see cref="OperationCanceledException"/> instead of running the remaining retry budget.</param>
    /// <returns>A task that completes when an attempt completes without throwing, or faults with the last attempt's
    /// exception once the retries are exhausted.</returns>
    Task WaitWithRetryAsync(
        Func<Task> action,
        IWaitSettings waitSettings,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the specified asynchronous action with retry logic and no cancellation token.
    /// </summary>
    /// <remarks>Equivalent to calling the three-argument overload with <see cref="CancellationToken.None"/>. The
    /// overloads are explicit rather than using an optional parameter so the public surface satisfies RS0026/RS0027.</remarks>
    /// <param name="action">The asynchronous action to execute. This action will be retried upon failure.</param>
    /// <param name="waitSettings">The settings that define the retry behavior.</param>
    /// <returns>A task that completes when the action succeeds or the retries are exhausted.</returns>
    Task WaitWithRetryAsync(
        Func<Task> action,
        IWaitSettings waitSettings);
}
