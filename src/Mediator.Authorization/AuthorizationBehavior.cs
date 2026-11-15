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

using System.Collections.Concurrent;
using System.Reflection;
using Boutquin.Abstractions.Auth;
using Boutquin.Domain.Abstractions;
using Boutquin.Mediator.Abstractions;
using Boutquin.Mediator.Abstractions.Extensions;
using Microsoft.Extensions.Logging;

namespace Boutquin.Mediator.Authorization;

// Boutquin.Mediator

/// <summary>
///     Represents a pipeline behavior that performs authorization checks on incoming requests before invoking the next
///     handler in the pipeline.
/// </summary>
/// <remarks>
///     This behavior enforces authorization by inspecting the request type for
///     <see
///         cref="AuthorizeAttribute" />
///     annotations. If authorization attributes are present, it performs the following checks:
///     <list type="bullet">
///         <item>
///             <description>Validates that the user is authenticated.</description>
///         </item>
///         <item>
///             <description>Performs role-based authorization if roles are specified.</description>
///         </item>
///         <item>
///             <description>Performs policy-based authorization if policies are specified.</description>
///         </item>
///     </list>
///     If
///     any of these checks fail, an appropriate failure response is returned. Otherwise, the request is passed  to the
///     next
///     handler in the pipeline.
/// </remarks>
/// <typeparam name="TRequest">The type of the request being processed. Must implement <see cref="IRequest{TResponse}" />.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the request. Must inherit from <see cref="Result" />.</typeparam>
public sealed class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<TRequest> _logger;

    /// <summary>Cache of <c>Result.Failure&lt;T&gt;(Error)</c> generic method instances, keyed by value type T.</summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_failureMethodCache = new();

    /// <summary>
    /// Initializes a new instance of <see cref="AuthorizationBehavior{TRequest, TResponse}"/>.
    /// </summary>
    /// <param name="currentUserService">
    ///     Service that provides access to information about the current user in the application context. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="authorizationService">
    ///     Service responsible for performing authorization checks, such as verifying user roles and evaluating authorization
    ///     policies. Cannot be <see langword="null" />.
    /// </param>
    /// <param name="logger">
    ///     The logger instance used to record diagnostic, informational, and error messages related to the authorization
    ///     process. Cannot be <see langword="null" />.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="currentUserService"/>, <paramref name="authorizationService"/>,
    ///     or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public AuthorizationBehavior(
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<TRequest> logger)
    {
        ArgumentNullException.ThrowIfNull(currentUserService);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(logger);

        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    ///     Handles the specified request by performing authorization checks and invoking the next handler in the pipeline.
    /// </summary>
    /// <remarks>
    ///     This method performs the following steps:
    ///     <list type="number">
    ///         <item>
    ///             <description>
    ///                 Checks for
    ///                 authorization attributes on the request type.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 If no authorization
    ///                 attributes are found, the request proceeds without authorization.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 If
    ///                 authorization attributes are present, it validates the user's authentication and performs role-based
    ///                 and
    ///                 policy-based authorization checks.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 If any authorization check fails,
    ///                 an appropriate failure response is returned. Otherwise, the next handler in the pipeline is invoked.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    /// <param name="request">The request to be processed. Cannot be <see langword="null" />.</param>
    /// <param name="next">
    ///     A delegate representing the next handler in the pipeline. This delegate is invoked if authorization
    ///     succeeds.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token to monitor for cancellation requests. The default value is
    ///     <see cref="CancellationToken.None" />.
    /// </param>
    /// <returns>
    ///     A <see cref="ValueTask{TResult}" /> that represents the asynchronous operation. The result contains the response
    ///     of type <typeparamref name="TResponse" />.
    /// </returns>
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        Func<ValueTask<TResponse>> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var requestName = typeof(TRequest).FullName ?? string.Empty;
        _logger.LogDebug("Authorizing request [{RequestName}].", requestName);

        // ---- AUTHORIZATION ATTRIBUTES ----
        var authorizeAttributes = typeof(TRequest)
            .GetCustomAttributes<AuthorizeAttribute>().ToArray();

        // If no authorization attributes are found, proceed without authorization
        if (authorizeAttributes.Length == 0)
        {
            _logger.LogDebug(
                "No authorization attributes found for request [{RequestName}]. Proceeding without authorization.",
                requestName);

            return await next.WithCancellation(cancellationToken, requestName, _logger).ConfigureAwait(false);
        }

        // ---- AUTHENTICATION ----
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogError(
                "User is not authenticated for request [{RequestName}]. Authorization failed.",
                requestName);

            return CreateFailureResponse(
                Error.Unauthorized("Error.Unauthorized", "The request requires user authentication."));
        }

        // ---- ROLES ----
        var roleResult = await CheckRoleBasedAuthorization(
            userId, authorizeAttributes, cancellationToken).ConfigureAwait(false);

        if (!roleResult.IsSuccess)
        {
            _logger.LogError(
                "User [{UserId}] is not authorized by role for request [{RequestName}]. Authorization failed.",
                userId, requestName);

            return CreateFailureResponse(roleResult.Error);
        }

        // ---- POLICIES ----
        var policyResult = await CheckPolicyBasedAuthorization(
            userId, authorizeAttributes, cancellationToken).ConfigureAwait(false);

        if (policyResult.IsSuccess)
        {
            return await next.WithCancellation(cancellationToken, requestName, _logger).ConfigureAwait(false);
        }

        _logger.LogError(
            "User [{UserId}] is not authorized by policy for request [{RequestName}]. Authorization failed.",
            userId, requestName);

        return CreateFailureResponse(policyResult.Error);
    }

    /// <summary>
    ///     Checks whether the specified user is authorized based on the required roles defined in the provided
    ///     <see
    ///         cref="AuthorizeAttribute" />
    ///     instances.
    /// </summary>
    /// <remarks>
    ///     This method evaluates the roles specified in the <paramref name="authorizeAttributes" />
    ///     parameter. If no roles are defined,  the method immediately returns a successful result. Otherwise, it checks
    ///     whether the user belongs to any of the required roles.
    /// </remarks>
    /// <param name="userId">The unique identifier of the user whose roles are being checked.</param>
    /// <param name="authorizeAttributes">
    ///     An array of <see cref="AuthorizeAttribute" /> instances that define the roles
    ///     required for authorization.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the operation to
    ///     complete.
    /// </param>
    /// <returns>
    ///     A <see cref="ValueTask{TResult}" /> that resolves to a <see cref="Result" /> indicating the outcome of the
    ///     authorization check. Returns <see cref="Result.Success" /> if the user is authorized or no roles are specified.
    ///     Returns <see cref="Result.Failure" /> if the user is not authorized or an error occurs during the check.
    /// </returns>
    private async ValueTask<Result> CheckRoleBasedAuthorization(
        string userId,
        AuthorizeAttribute[] authorizeAttributes,
        CancellationToken cancellationToken)
    {
        // Extract attributes with roles
        var roleAttributes = authorizeAttributes
            .Where(a => !string.IsNullOrWhiteSpace(a.RequiredRoles))
            .ToArray();

        // If no roles are specified, return success immediately
        if (roleAttributes.Length == 0)
        {
            return Result.Success();
        }

        // Extract roles from the authorize attributes
        var requiredRoles = roleAttributes
            .SelectMany(a => a.RequiredRoles.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(r => r.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();

        // Check if the user is in any of the required roles
        var result = await _authorizationService.IsInAnyRoleAsync(userId, requiredRoles, cancellationToken).ConfigureAwait(false);

        // Return the result based on the role check
        return result.IsSuccess
            ? result.Value
                ? Result.Success()
                : Result.Failure(
                    Error.Forbidden(
                        "Error.Forbidden.Role",
                        $"User [{userId}] is not authorized for required roles: [{string.Join(", ", requiredRoles)}]."))
            : Result.Failure(result.Error);
    }

    /// <summary>
    ///     Evaluates the specified authorization policies for a user and determines whether access is granted.
    /// </summary>
    /// <remarks>
    ///     This method evaluates the policies specified in the <paramref name="authorizeAttributes" />
    ///     parameter.  Policies are extracted from the <see cref="AuthorizeAttribute.Policy" /> property, and multiple
    ///     policies  can be specified as a comma-separated list. The method ensures that all specified policies are
    ///     distinct  and case-insensitive.  If no policies are specified, the method returns <see cref="Result.Success" />
    ///     immediately. Otherwise, it delegates the authorization check to the underlying
    ///     <c>authorizationService</c>.
    /// </remarks>
    /// <param name="userId">The unique identifier of the user to be authorized.</param>
    /// <param name="authorizeAttributes">
    ///     An array of <see cref="AuthorizeAttribute" /> instances specifying the policies to
    ///     evaluate.
    /// </param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the operation to
    ///     complete.
    /// </param>
    /// <returns>
    ///     A <see cref="ValueTask{TResult}" /> containing a <see cref="Result" /> that indicates the outcome of the
    ///     authorization check. Returns <see cref="Result.Success" /> if the user is authorized for all specified policies.
    ///     Returns <see cref="Result.Failure" /> if the user is not authorized or if an error occurs during the
    ///     authorization process.
    /// </returns>
    private async ValueTask<Result> CheckPolicyBasedAuthorization(
        string userId,
        AuthorizeAttribute[] authorizeAttributes,
        CancellationToken cancellationToken)
    {
        // Extract attributes with policies
        var policyAttributes = authorizeAttributes
            .Where(a => !string.IsNullOrWhiteSpace(a.Policy))
            .ToArray();

        // If no policies are specified, return success immediately
        if (policyAttributes.Length == 0)
        {
            return Result.Success();
        }

        // Extract policies from the authorize attributes
        var policies = policyAttributes
            .SelectMany(a => a.Policy.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(p => p.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();

        // Check if the user is authorized for the specified policies
        var result = await _authorizationService.AuthorizeAsync(userId, policies, cancellationToken).ConfigureAwait(false);

        // Return the result based on the authorization check
        return result.IsSuccess
            ? result.Value
                ? Result.Success()
                : Result.Failure(
                    Error.Forbidden(
                        "Error.Forbidden.Policy",
                        $"User [{userId}] is not authorized for policies: [{string.Join(", ", policies)}]."))
            : Result.Failure(result.Error);
    }

    /// <summary>
    /// Creates a typed failure response of <typeparamref name="TResponse"/> (either <see cref="Result"/> or
    /// <see cref="Result{T}"/>), using cached reflection for the generic case.
    /// </summary>
    private static TResponse CreateFailureResponse(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var method = s_failureMethodCache.GetOrAdd(
            valueType,
            t => typeof(Result).GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!.MakeGenericMethod(t));
        return (TResponse)method.Invoke(null, [error])!;
    }
}
