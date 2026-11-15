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

namespace Boutquin.Mediator.Reliability;

using System;
using System.Collections.Generic;

using Boutquin.Reliability;

/// <summary>
/// Represents the configuration settings for retry logic in operations that may fail and require retries.
/// </summary>
/// <remarks>
/// This class provides a flexible way to configure retry behavior, including the number of retries,
/// timeout per attempt, and the delay between retries. It also supports enabling or disabling retries
/// and determining whether to continue execution after a timeout occurs.
/// <para>
/// Exposed as a sealed mutable class (rather than a positional record) so that
/// <see cref="Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Microsoft.Extensions.Configuration.IConfiguration, object?)"/> can populate
/// instances created by the binder's parameterless constructor at runtime.
/// </para>
/// </remarks>
public sealed class RetrySettings : IWaitSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether retry logic is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the operation will attempt retries according to the specified settings;
    /// <c>false</c> if retries are disabled.
    /// </value>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operation should continue execution
    /// after a timeout occurs.
    /// </summary>
    /// <value>
    /// <c>true</c> if the operation will proceed with subsequent retries even if a timeout
    /// is encountered; otherwise, <c>false</c>.
    /// </value>
    public bool ContinueAfterTimeout { get; set; }

    private int _numberOfRetries = 2;

    /// <summary>
    /// Gets or sets the total number of retry attempts allowed for a failed operation.
    /// </summary>
    /// <value>
    /// The number of times the operation will be retried before giving up. Any negative value
    /// (from configuration or a direct set) is coerced to <c>0</c>, both because a negative retry
    /// count is meaningless and because Polly's <c>WaitAndRetryAsync</c> requires a non-negative
    /// <c>retryCount</c> and throws <see cref="ArgumentOutOfRangeException"/> at policy build otherwise.
    /// </value>
    public int NumberOfRetries
    {
        get => _numberOfRetries;
        set => _numberOfRetries = value < 0 ? 0 : value;
    }

    /// <summary>
    /// Gets or sets the list of sleep durations, in milliseconds, used for configuring
    /// delays between retries.
    /// </summary>
    /// <value>
    /// The delay for each attempt is determined by the corresponding value in this list,
    /// or the last value if the attempt index exceeds the list length.
    /// </value>
    public IReadOnlyList<int>? SleepDurationsMs { get; set; }

    private TimeSpan _timeoutPerAttempt = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum duration allowed for each individual attempt of the operation.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> representing the per-attempt timeout. Any non-positive value
    /// (<see cref="TimeSpan.Zero"/> or negative) is coerced to 10 seconds, both to prevent an
    /// immediate timeout and because Polly's <c>Policy.TimeoutAsync</c> requires a strictly
    /// positive timeout and throws <see cref="ArgumentOutOfRangeException"/> otherwise.
    /// </value>
    public TimeSpan TimeoutPerAttempt
    {
        get => _timeoutPerAttempt;
        set => _timeoutPerAttempt = value <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : value;
    }

    private static readonly IReadOnlyList<int> s_defaultSleepDurations = [100];

    /// <inheritdoc/>
    /// <remarks>
    /// When <see cref="SleepDurationsMs"/> is <see langword="null"/> <em>or</em> an empty list, the
    /// provider falls back to <see cref="s_defaultSleepDurations"/> rather than indexing the empty
    /// collection: a consumer that supplies an empty list through the public <c>RetrySettings</c> /
    /// <c>IWaitWithRetry</c> surface would otherwise trigger an <see cref="ArgumentOutOfRangeException"/>
    /// on the first retry.
    /// </remarks>
    public Func<int, TimeSpan> SleepTimeDurationProvider =>
        attempt =>
        {
            var durations = SleepDurationsMs is { Count: > 0 } d ? d : s_defaultSleepDurations;
            return TimeSpan.FromMilliseconds(durations[Math.Min(attempt - 1, durations.Count - 1)]);
        };

    /// <summary>
    /// Gets the list of sleep durations, in milliseconds, used for configuring delays.
    /// </summary>
    public IReadOnlyList<int> SleepDurations =>
        SleepDurationsMs is { Count: > 0 } d ? d : s_defaultSleepDurations;
}
