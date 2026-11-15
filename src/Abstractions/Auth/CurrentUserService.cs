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

using System.Security.Claims;

namespace Boutquin.Abstractions.Auth;

/// <summary>
/// Provides functionality to retrieve information about the current user.
/// </summary>
/// <remarks>This service is typically used to access the unique identifier of the currently authenticated user.
/// It reads the supplied <see cref="ClaimsPrincipal"/> to extract user information.</remarks>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly Func<ClaimsPrincipal?> _principalAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrentUserService"/> class.
    /// </summary>
    /// <param name="principalAccessor">
    /// A delegate that returns the <see cref="ClaimsPrincipal"/> for the current request/context,
    /// or <see langword="null"/> when no principal is available.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="principalAccessor"/> is <see langword="null"/>.
    /// </exception>
    public CurrentUserService(Func<ClaimsPrincipal?> principalAccessor)
        => _principalAccessor = principalAccessor ?? throw new ArgumentNullException(nameof(principalAccessor));

    /// <inheritdoc/>
    public string? UserId => _principalAccessor()?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
