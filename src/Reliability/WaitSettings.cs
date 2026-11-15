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

/// <summary>
/// Represents configuration settings for wait operations, including retry behavior and timeout handling.
/// </summary>
/// <remarks>
/// <para>This type encapsulates settings for operations that involve waiting or retrying, such as specifying
/// the number of retries, the timeout duration for each attempt, and the sleep duration between retries. It is designed
/// to provide flexibility for customizing wait behavior in scenarios where retries are necessary.</para>
/// <para>
/// Construction-time guards mirror the coercion applied by the sibling
/// <c>Boutquin.Mediator.Reliability.RetrySettings</c> class (in the <c>Boutquin.Mediator.Reliability</c> package) so that both
/// <see cref="IWaitSettings"/> implementations behave identically at their boundaries:
/// <list type="bullet">
///   <item><description>
///     <see cref="SleepTimeDurationProvider"/> — <see langword="null"/> is rejected with
///     <see cref="ArgumentNullException"/> at construction time and also when assigning via
///     a <see langword="with"/> expression, because a null delegate cannot be safely coerced
///     and would throw <see cref="NullReferenceException"/> inside Polly's retry loop.
///   </description></item>
///   <item><description>
///     <see cref="NumberOfRetries"/> — any value less than zero is coerced to <c>0</c>
///     because Polly's <c>WaitAndRetryAsync</c> requires a non-negative <c>retryCount</c>
///     and throws <see cref="ArgumentOutOfRangeException"/> otherwise.
///   </description></item>
///   <item><description>
///     <see cref="TimeoutPerAttempt"/> — any non-positive value (<see cref="TimeSpan.Zero"/>
///     or negative) is coerced to <c>10 seconds</c> because Polly's <c>Policy.TimeoutAsync</c>
///     requires a strictly positive timeout and throws <see cref="ArgumentOutOfRangeException"/>
///     otherwise.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="ContinueAfterTimeout">A value indicating whether the operation should continue processing after the timeout for an attempt has elapsed.
/// <see langword="true"/> if the operation should continue; otherwise, <see langword="false"/>.</param>
/// <param name="SleepTimeDurationProvider">A function that determines the duration to wait between retries based on the current retry attempt index. The
/// function takes an integer representing the retry attempt index (starting at 0) and returns a <see cref="TimeSpan"/>
/// specifying the sleep duration. Must not be <see langword="null"/>.</param>
/// <param name="NumberOfRetries">The maximum number of retry attempts allowed. Any negative value is coerced to <c>0</c>.</param>
/// <param name="TimeoutPerAttempt">The maximum duration allowed for each individual attempt before timing out. Any non-positive value
/// (<see cref="TimeSpan.Zero"/> or negative) is coerced to <c>10 seconds</c>.</param>
public sealed record WaitSettings(
    bool ContinueAfterTimeout,
    Func<int, TimeSpan> SleepTimeDurationProvider,
    int NumberOfRetries,
    TimeSpan TimeoutPerAttempt) : IWaitSettings
{
    private Func<int, TimeSpan> _sleepTimeDurationProvider =
        SleepTimeDurationProvider ?? throw new ArgumentNullException(nameof(SleepTimeDurationProvider));
    private int _numberOfRetries = NumberOfRetries < 0 ? 0 : NumberOfRetries;
    private TimeSpan _timeoutPerAttempt =
        TimeoutPerAttempt <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : TimeoutPerAttempt;

    /// <summary>
    /// Gets or inits a function that determines the duration to wait between retries based on the current retry attempt index.
    /// </summary>
    /// <value>
    /// A delegate that maps a retry-attempt index (starting at <c>1</c>) to a <see cref="TimeSpan"/> sleep duration.
    /// Must not be <see langword="null"/>; a <see langword="null"/> value throws <see cref="ArgumentNullException"/>
    /// both at primary construction and in <see langword="with"/>-expression assignment.
    /// </value>
    public Func<int, TimeSpan> SleepTimeDurationProvider
    {
        get => _sleepTimeDurationProvider;
        init => _sleepTimeDurationProvider = value ?? throw new ArgumentNullException(nameof(SleepTimeDurationProvider));
    }

    /// <summary>
    /// Gets or inits the maximum number of retry attempts allowed.
    /// </summary>
    /// <value>
    /// The number of times the operation will be retried before giving up. Any negative value
    /// (from the primary constructor or a <see langword="with"/>-expression assignment) is coerced to <c>0</c>,
    /// because Polly's <c>WaitAndRetryAsync</c> requires a non-negative <c>retryCount</c> and throws
    /// <see cref="ArgumentOutOfRangeException"/> otherwise.
    /// </value>
    public int NumberOfRetries
    {
        get => _numberOfRetries;
        init => _numberOfRetries = value < 0 ? 0 : value;
    }

    /// <summary>
    /// Gets or inits the maximum duration allowed for each individual attempt before timing out.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> representing the per-attempt timeout. Any non-positive value
    /// (<see cref="TimeSpan.Zero"/> or negative) is coerced to <c>10 seconds</c>, both to prevent an
    /// immediate timeout and because Polly's <c>Policy.TimeoutAsync</c> requires a strictly positive
    /// timeout and throws <see cref="ArgumentOutOfRangeException"/> otherwise.
    /// </value>
    public TimeSpan TimeoutPerAttempt
    {
        get => _timeoutPerAttempt;
        init => _timeoutPerAttempt = value <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : value;
    }
}
