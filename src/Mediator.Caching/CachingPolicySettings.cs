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

namespace Boutquin.Mediator.Caching;

/// <summary>
/// Represents the configuration settings for a caching policy.
/// </summary>
/// <remarks>
/// This class encapsulates various options for controlling caching behavior, including whether caching
/// is disabled, expiration policies, and whether the cache is scoped per user.
/// <para>
/// Exposed as a mutable class (rather than a positional record) so that
/// <see cref="Microsoft.Extensions.Configuration.ConfigurationBinder.Bind(Microsoft.Extensions.Configuration.IConfiguration, object?)"/> can populate
/// instances created by the binder's parameterless constructor at runtime.
/// </para>
/// </remarks>
public sealed class CachingPolicySettings
{
    /// <summary>
    /// Gets or sets a value indicating whether caching is disabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if no caching will be performed and all requests will bypass the cache;
    /// otherwise, <c>false</c>.
    /// </value>
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the cache should be reset or invalidated
    /// when this policy is applied.
    /// </summary>
    /// <value>
    /// <c>true</c> if any existing cached entries will be cleared; otherwise, <c>false</c>.
    /// </value>
    public bool Reset { get; set; }

    /// <summary>
    /// Gets or sets the time interval after which a cached entry should expire,
    /// measured from the moment it is added to the cache.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> representing the absolute expiration window, or
    /// <c>null</c> if absolute expiration is not applied.
    /// </value>
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

    /// <summary>
    /// Gets or sets the time interval that resets every time the cached entry is accessed.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> representing the sliding expiration window, or
    /// <c>null</c> if sliding expiration is not applied.
    /// If the entry is not accessed within this period, it will expire.
    /// </value>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the cache is scoped per user.
    /// </summary>
    /// <value>
    /// <c>true</c> if each user will have a separate cache entry for the same data;
    /// <c>false</c> if the cache is shared across all users.
    /// </value>
    public bool PerUser { get; set; }

    /// <summary>
    /// Gets the default caching policy settings.
    /// </summary>
    public static CachingPolicySettings Default => new()
    {
        Disabled = false,
        Reset = false,
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        SlidingExpiration = TimeSpan.FromSeconds(30),
        PerUser = false
    };
}
