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
/// Pins the construction-time input guards added to <see cref="WaitSettings"/>
/// (F-W3-01): coercion of <see cref="WaitSettings.NumberOfRetries"/> and
/// <see cref="WaitSettings.TimeoutPerAttempt"/>, and null rejection of
/// <see cref="WaitSettings.SleepTimeDurationProvider"/>.
/// </summary>
/// <remarks>
/// These tests mirror the analogous guards that <see cref="Boutquin.Mediator.Reliability.RetrySettings"/>
/// provides for the same <see cref="IWaitSettings"/> fields, ensuring the two concrete
/// implementations of <see cref="IWaitSettings"/> behave identically at their boundaries.
/// </remarks>
public sealed class WaitSettingsTests
{
    private static readonly Func<int, TimeSpan> s_validProvider = _ => TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan s_validTimeout = TimeSpan.FromSeconds(5);

    // -----------------------------------------------------------------------
    // WS-GUARD-01: NumberOfRetries < 0  →  coerced to 0
    // -----------------------------------------------------------------------

    /// <summary>
    /// WS-GUARD-01: A negative <see cref="WaitSettings.NumberOfRetries"/> must be coerced
    /// to <c>0</c> at construction time, matching <see cref="Boutquin.Mediator.Reliability.RetrySettings"/>
    /// behaviour and preventing a Polly <see cref="ArgumentOutOfRangeException"/> at policy build.
    /// </summary>
    [Fact]
    public void WaitSettings_NumberOfRetries_CoercesNegativeToZero()
    {
        // Act
        var settings = new WaitSettings(false, s_validProvider, -1, s_validTimeout);

        // Assert — must be coerced to 0, not -1
        Assert.Equal(0, settings.NumberOfRetries);
    }

    // -----------------------------------------------------------------------
    // WS-GUARD-02: TimeoutPerAttempt <= Zero  →  coerced to 10 s
    // -----------------------------------------------------------------------

    /// <summary>
    /// WS-GUARD-02: A <see cref="WaitSettings.TimeoutPerAttempt"/> of <see cref="TimeSpan.Zero"/>
    /// must be coerced to <c>10 seconds</c>, matching <see cref="Boutquin.Mediator.Reliability.RetrySettings"/>
    /// and preventing a Polly <see cref="ArgumentOutOfRangeException"/> at policy build.
    /// </summary>
    [Fact]
    public void WaitSettings_TimeoutPerAttempt_CoercesZeroToTenSeconds()
    {
        // Act
        var settings = new WaitSettings(false, s_validProvider, 3, TimeSpan.Zero);

        // Assert — must be coerced to 10 s
        Assert.Equal(TimeSpan.FromSeconds(10), settings.TimeoutPerAttempt);
    }

    /// <summary>
    /// WS-GUARD-02b: A negative <see cref="WaitSettings.TimeoutPerAttempt"/> must also be coerced
    /// to <c>10 seconds</c>, consistent with <see cref="Boutquin.Mediator.Reliability.RetrySettings"/>
    /// handling of any non-positive value.
    /// </summary>
    [Fact]
    public void WaitSettings_TimeoutPerAttempt_CoercesNegativeToTenSeconds()
    {
        // Act
        var settings = new WaitSettings(false, s_validProvider, 3, TimeSpan.FromSeconds(-5));

        // Assert — must be coerced to 10 s
        Assert.Equal(TimeSpan.FromSeconds(10), settings.TimeoutPerAttempt);
    }

    // -----------------------------------------------------------------------
    // WS-GUARD-03: SleepTimeDurationProvider == null  →  ArgumentNullException
    // -----------------------------------------------------------------------

    /// <summary>
    /// WS-GUARD-03: A <see langword="null"/> <see cref="WaitSettings.SleepTimeDurationProvider"/>
    /// must throw <see cref="ArgumentNullException"/> at construction time, preventing a
    /// <see cref="NullReferenceException"/> inside Polly's retry loop when the delegate is invoked.
    /// </summary>
    [Fact]
    public void WaitSettings_NullSleepTimeDurationProvider_ThrowsArgumentNullException()
    {
        // Act & Assert — null provider cannot be coerced; must fail fast at construction
        Assert.Throws<ArgumentNullException>(
            () => new WaitSettings(false, null!, 3, s_validTimeout));
    }

    // -----------------------------------------------------------------------
    // WS-GUARD-04: valid inputs are preserved unchanged
    // -----------------------------------------------------------------------

    /// <summary>
    /// WS-GUARD-04: Valid construction arguments must be stored unchanged —
    /// the guard must not coerce values that already satisfy the contracts.
    /// </summary>
    [Fact]
    public void WaitSettings_ValidInputs_ArePreservedUnchanged()
    {
        // Arrange
        const int retries = 3;
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var settings = new WaitSettings(true, s_validProvider, retries, timeout);

        // Assert
        Assert.True(settings.ContinueAfterTimeout);
        Assert.Equal(retries, settings.NumberOfRetries);
        Assert.Equal(timeout, settings.TimeoutPerAttempt);
        Assert.Same(s_validProvider, settings.SleepTimeDurationProvider);
    }
}
