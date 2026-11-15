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
using Boutquin.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace Boutquin.Mediator.Abstractions.Extensions;

/// <summary>
/// Mediator-specific extensions for the Result type.
/// </summary>
public static class ResultExtensions
{
    /// <summary>Cache of <c>Result.Failure&lt;T&gt;(Error)</c> generic method instances, keyed by value type T.</summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_failureMethodCache = new();

    /// <summary>
    /// Executes the specified asynchronous operation, returning a cancellation result if the operation is cancelled.
    /// </summary>
    public static async ValueTask<TResponse> WithCancellation<TResponse>(
        this Func<ValueTask<TResponse>> next,
        CancellationToken cancellationToken,
        string requestName,
        ILogger logger,
        string code = "Error.Cancellation",
        string name = "The request was cancelled."
    ) where TResponse : Result
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            return await next().ConfigureAwait(false);
        }

        logger.LogInformation(
            "Request [{RequestName}] was cancelled.",
            requestName);

        return CreateCancelledResponse<TResponse>(code, name);
    }

    /// <summary>
    /// Creates a cancelled (failed) response of <typeparamref name="TResponse"/> (either
    /// <see cref="Result"/> or <see cref="Result{T}"/>), using cached reflection for the generic
    /// case. Mirrors <see cref="Result.Cancelled(string, string)"/>, which maps to an
    /// <see cref="Error.RequestTimeout(string, string)"/> failure, but constructs the result as
    /// the actual closed response type rather than downcasting a base <see cref="Result"/>.
    /// </summary>
    private static TResponse CreateCancelledResponse<TResponse>(string code, string name)
        where TResponse : Result
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Cancelled(code, name);
        }

        var error = Error.RequestTimeout(code, name);
        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var method = s_failureMethodCache.GetOrAdd(
            valueType,
            t => typeof(Result).GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!.MakeGenericMethod(t));
        return (TResponse)method.Invoke(null, [error])!;
    }
}
