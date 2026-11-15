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

namespace Boutquin.Abstractions.Auth;

using Boutquin.Domain.Abstractions;

/// <summary>
/// Defines methods for performing authorization checks, such as verifying user roles or evaluating policies.
/// </summary>
/// <remarks>This interface provides asynchronous methods to determine whether a user is in a specific role or
/// satisfies a given authorization policy. Implementations of this interface should handle the underlying logic for
/// role and policy evaluation, such as querying a database or integrating with an external identity provider.</remarks>
public interface IAuthorizationService
{
    /// <summary>
    /// Determines whether the specified user is in any of the provided roles.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to check. Cannot be <see langword="null"/> or empty.</param>
    /// <param name="roles">A collection of role names to check against. Cannot be <see langword="null"/> and must contain at least one
    /// role.</param>
    /// <param name="cancellationToken">An optional token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to <see langword="true"/> if the user is in at least one of the
    /// specified roles; otherwise, <see langword="false"/>.</returns>
    ValueTask<Result<bool>> IsInAnyRoleAsync(string userId, IEnumerable<string> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously evaluates whether the specified user is authorized based on the provided policies.
    /// </summary>
    /// <remarks>This method evaluates all specified policies and determines whether the user satisfies the
    /// requirements for each. The operation is non-blocking and supports cancellation through the <paramref
    /// name="cancellationToken"/> token.</remarks>
    /// <param name="userId">The unique identifier of the user to evaluate. Cannot be <see langword="null"/> or empty.</param>
    /// <param name="policies">A collection of policy names to check against. Cannot be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">An optional token to observe while waiting for the operation to complete.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to <see langword="true"/> if the user is authorized for all
    /// specified policies; otherwise, <see langword="false"/>.</returns>
    ValueTask<Result<bool>> AuthorizeAsync(string userId, IEnumerable<string> policies, CancellationToken cancellationToken = default);
}
