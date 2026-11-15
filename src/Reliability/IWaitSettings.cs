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
/// Defines the settings used to configure wait and retry behavior for operations.
/// </summary>
/// <remarks>This interface provides properties to control the behavior of operations that involve retries,
/// including the number of retries, the duration between retries, and whether to continue execution after a timeout
/// occurs. Implementations of this interface can be used to customize retry logic for various scenarios.</remarks>
public interface IWaitSettings
{
    /// <summary>
    /// Gets a value indicating whether the operation should continue execution after a timeout occurs.
    /// </summary>
    bool ContinueAfterTimeout { get; }

    /// <summary>
    /// Gets a function that determines the sleep duration based on the current retry attempt.
    /// </summary>
    Func<int, TimeSpan> SleepTimeDurationProvider { get; }

    /// <summary>
    /// Gets the number of retry attempts allowed for a failed operation.
    /// </summary>
    int NumberOfRetries { get; }

    /// <summary>
    /// Gets the maximum duration allowed for each individual attempt of an operation.
    /// </summary>
    TimeSpan TimeoutPerAttempt { get; }
}
