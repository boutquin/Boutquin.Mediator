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

using System.Globalization;
using Boutquin.Mediator.Caching;
using Boutquin.Mediator.Reliability;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Proves that <c>IConfiguration.Bind</c> correctly mutates
/// <see cref="CachingPolicySettings"/> (CR-14) and <see cref="RetrySettings"/> (CR-15)
/// when both types expose <c>{ get; set; }</c> properties.
/// </summary>
/// <remarks>
/// Tests in this class are RED while the types are positional records,
/// because the parameterless constructor <c>new CachingPolicySettings()</c> and
/// <c>new RetrySettings()</c> used by <c>Bind()</c> do not exist on positional records.
/// They turn GREEN once both types are converted to classes with a parameterless
/// constructor and <c>{ get; set; }</c> properties.
/// </remarks>
public sealed class ConfigBindingTests
{
    // ----------------------------------------------------------------
    // CR-14: CachingPolicySettings
    // ----------------------------------------------------------------

    /// <summary>
    /// CR-14: <c>IConfiguration.Bind()</c> must populate a fresh
    /// <see cref="CachingPolicySettings"/> instance (created via its parameterless
    /// constructor) with <see cref="CachingPolicySettings.Disabled"/> set to <c>true</c>.
    /// </summary>
    [Fact]
    public void Bind_WithDisabledTrue_SetsCachingPolicySettingsDisabled()
    {
        // Arrange — in-memory config with Disabled=true
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Disabled"] = "true"
            })
            .Build();

        // A fresh instance via parameterless ctor — this is what Bind() instantiates
        // internally. If the type is a positional record this line fails to compile.
        var settings = new CachingPolicySettings();

        // Act
        config.Bind(settings);

        // Assert
        Assert.True(settings.Disabled,
            "Bind() must be able to set Disabled=true on a fresh CachingPolicySettings instance.");
    }

    /// <summary>
    /// CR-14: <c>CachingPolicySettings.Default</c> must produce a mutable instance
    /// whose properties can be overwritten by <c>Bind()</c>, allowing per-request
    /// config overrides to take effect at runtime.
    /// </summary>
    [Fact]
    public void Bind_OverridesDefaultSettings_OnCachingPolicySettings()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Disabled"] = "true",
                ["PerUser"] = "true"
            })
            .Build();

        var settings = CachingPolicySettings.Default; // Disabled and PerUser start false

        // Act
        config.Bind(settings);

        // Assert
        Assert.True(settings.Disabled, "Bind() must overwrite Disabled=true on a Default instance.");
        Assert.True(settings.PerUser, "Bind() must overwrite PerUser=true on a Default instance.");
    }

    // ----------------------------------------------------------------
    // CR-15: RetrySettings
    // ----------------------------------------------------------------

    /// <summary>
    /// CR-15: <c>IConfiguration.Bind()</c> must populate a fresh
    /// <see cref="RetrySettings"/> instance (created via its parameterless constructor)
    /// with a non-default <see cref="RetrySettings.NumberOfRetries"/> value.
    /// </summary>
    [Fact]
    public void Bind_WithNumberOfRetries_SetsRetrySettingsCount()
    {
        // Arrange
        const int expectedRetries = 5;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NumberOfRetries"] = expectedRetries.ToString(CultureInfo.InvariantCulture)
            })
            .Build();

        // Parameterless ctor — fails to compile if RetrySettings is still a positional record
        var settings = new RetrySettings();

        // Act
        config.Bind(settings);

        // Assert
        Assert.Equal(expectedRetries, settings.NumberOfRetries);
    }

    /// <summary>
    /// CR-15: <see cref="RetrySettings.TimeoutPerAttempt"/> setter must default a
    /// <see cref="TimeSpan.Zero"/> value to 10 seconds, matching the historical
    /// positional-record init logic.
    /// </summary>
    [Fact]
    public void RetrySettings_TimeoutPerAttempt_DefaultsZeroToTenSeconds()
    {
        // Arrange — a settings object with TimeoutPerAttempt bound to zero
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TimeoutPerAttempt"] = "00:00:00"  // TimeSpan.Zero
            })
            .Build();

        var settings = new RetrySettings();

        // Act
        config.Bind(settings);

        // Assert — zero must be normalized to 10 s
        Assert.Equal(TimeSpan.FromSeconds(10), settings.TimeoutPerAttempt);
    }

    /// <summary>
    /// CR-15: <see cref="RetrySettings.TimeoutPerAttempt"/> setter must preserve a
    /// non-zero value provided via <c>Bind()</c>.
    /// </summary>
    [Fact]
    public void RetrySettings_TimeoutPerAttempt_PreservesNonZeroValue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TimeoutPerAttempt"] = "00:00:05"  // 5 seconds
            })
            .Build();

        var settings = new RetrySettings();

        // Act
        config.Bind(settings);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), settings.TimeoutPerAttempt);
    }

    /// <summary>
    /// RETRY-NEGRETRIES-001 (F-V2-02): <see cref="RetrySettings.NumberOfRetries"/> setter must
    /// coerce a NEGATIVE value (bound from a config value such as <c>"-1"</c>) to a non-negative
    /// floor, just as <see cref="RetrySettings.TimeoutPerAttempt"/> coerces a non-positive timeout.
    /// A negative <c>retryCount</c> otherwise reaches Polly's <c>WaitAndRetryAsync</c>, which
    /// requires <c>&gt;= 0</c> and throws <see cref="ArgumentOutOfRangeException"/> at policy build.
    /// </summary>
    [Fact]
    public void RetrySettings_NumberOfRetries_CoercesNegativeToZero()
    {
        // Arrange — config binds a negative retry count
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NumberOfRetries"] = "-1"
            })
            .Build();

        var settings = new RetrySettings();

        // Act
        config.Bind(settings);

        // Assert — a negative count must be normalized to 0
        Assert.Equal(0, settings.NumberOfRetries);
    }

    /// <summary>
    /// RETRY-NEGTIMEOUT-001: <see cref="RetrySettings.TimeoutPerAttempt"/> setter must
    /// coerce a NEGATIVE <see cref="TimeSpan"/> (bound from a config value such as
    /// <c>"-00:00:05"</c>) to the 10-second default, just as it does for
    /// <see cref="TimeSpan.Zero"/>. A non-positive timeout otherwise reaches Polly's
    /// <c>Policy.TimeoutAsync</c>, which requires <c>&gt; Zero</c> and throws
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void RetrySettings_TimeoutPerAttempt_CoercesNegativeToTenSeconds()
    {
        // Arrange — a settings object with TimeoutPerAttempt bound to a negative value
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TimeoutPerAttempt"] = "-00:00:05"  // negative 5 seconds
            })
            .Build();

        var settings = new RetrySettings();

        // Act
        config.Bind(settings);

        // Assert — a non-positive timeout must be normalized to 10 s
        Assert.Equal(TimeSpan.FromSeconds(10), settings.TimeoutPerAttempt);
    }

    /// <summary>
    /// RETRY-NEGTIMEOUT-001: an enabled retry whose <see cref="RetrySettings.TimeoutPerAttempt"/>
    /// was bound to a negative value must NOT cause Polly to throw
    /// <see cref="ArgumentOutOfRangeException"/> — the coerced 10 s default keeps the
    /// timeout positive, so a successful action completes normally.
    /// </summary>
    [Fact]
    public async Task RetrySettings_NegativeTimeout_DoesNotThrowArgumentOutOfRange()
    {
        // Arrange — bind a negative timeout, then drive it through the real Polly path
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TimeoutPerAttempt"] = "-00:00:05"
            })
            .Build();

        var settings = new RetrySettings { Enabled = true };
        config.Bind(settings);

        var (sut, _) = Helpers.WaitWithRetryFixture.Create();

        // Act — a fast successful action; the only way this throws is a non-positive timeout
        var actual = await sut.WaitWithRetryAsync(() => Task.FromResult(7), settings).ConfigureAwait(true);

        // Assert
        Assert.Equal(7, actual);
    }
}
