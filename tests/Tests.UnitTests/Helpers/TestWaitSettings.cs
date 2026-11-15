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

namespace Boutquin.Tests.UnitTests.Helpers;

/// <summary>
/// Represents configuration settings for retry and timeout behavior during wait operations.
/// </summary>
/// <remarks>This type provides settings for controlling the number of retries, the duration between retries,  and
/// the behavior when a timeout occurs. It is designed to be used in scenarios where wait operations  require
/// customizable retry logic and timeout handling.</remarks>
internal sealed record TestWaitSettings : IWaitSettings
{
    /// <summary>
    /// Gets the number of retry attempts allowed for an operation.
    /// </summary>
    public int NumberOfRetries { get; init; } = 3;
    /// <summary>
    /// Gets a function that determines the duration of sleep time based on a given retry attempt.
    /// </summary>
    public Func<int, TimeSpan> SleepTimeDurationProvider { get; init; } = _ => TimeSpan.Zero;
    /// <summary>
    /// Gets a value indicating whether the operation should continue execution after a timeout occurs.
    /// </summary>
    public bool ContinueAfterTimeout { get; init; }
    /// <summary>
    /// Gets the timeout duration for each individual attempt in a retry operation.
    /// </summary>
    // Generous default so tests that are not about timeouts cannot trip Polly's per-attempt
    // timeout when Task.Run scheduling stalls under a loaded, parallel test run (15 ms flaked).
    // Timeout tests set an explicit short TimeoutPerAttempt.
    public TimeSpan TimeoutPerAttempt { get; init; } = TimeSpan.FromSeconds(5);
}
