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

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
// Boutquin.Mediator
using Abstractions;
using Abstractions.Extensions;
using Boutquin.Abstractions.Auth;
using Boutquin.Domain.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents a pipeline behavior that applies caching logic to requests and responses.
/// </summary>
/// <remarks>This class implements caching for requests and responses in a pipeline. It uses a distributed cache
/// to store responses based on a generated cache key. The caching behavior is determined by the presence of a  <see
/// cref="CachingPolicyAttribute"/> on the request type and the associated caching configuration. <para> If caching is
/// enabled, the behavior is as follows: <list type="bullet"> <item><description>Attempts to retrieve a cached response
/// using a generated cache key.</description></item> <item><description>If a cached response is found, it is returned
/// immediately, bypassing further processing.</description></item> <item><description>If no cached response is found,
/// the request is processed by the next handler, and a successful response is cached for future use. A failure
/// <see cref="Result"/> is returned to the caller but never cached, so a transient error does not outlive its
/// cause.</description></item>
/// </list> </para> The cache key can include user-specific information if per-user caching is enabled. Cache entries
/// can be configured with absolute and sliding expiration policies.</remarks>
/// <typeparam name="TRequest">The type of the request. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of the response. Must inherit from <see cref="Result"/>.</typeparam>
/// <param name="cache">
/// The distributed cache instance used to store and retrieve cached responses. This cache is responsible for persisting
/// response data across requests and, depending on configuration, across application instances.
/// </param>
/// <param name="currentUserService">
/// Service providing access to information about the current user, such as the user identifier. Used to support per-user
/// caching scenarios where cache entries are scoped to individual users.
/// </param>
/// <param name="configuration">
/// The application configuration provider used to retrieve caching policy settings for specific request types. This allows
/// caching behavior to be customized via configuration files or other configuration sources.
/// </param>
/// <param name="logger">
/// Logger instance for recording diagnostic and operational information related to caching behavior, such as cache hits,
/// misses, and errors.
/// </param>
public sealed class CachingBehavior<TRequest, TResponse>(
    IDistributedCache cache,
    ICurrentUserService currentUserService,
    IConfiguration configuration,
    ILogger<TRequest> logger
    ) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : Result
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new ResultJsonConverterFactory() }
    };

    /// <summary>Cache of <c>Result.Failure&lt;T&gt;(Error)</c> generic method instances, keyed by value type T.</summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_failureMethodCache = new();

    /// <summary>
    /// Creates a typed failure response of <typeparamref name="TResponse"/> (either <see cref="Result"/> or
    /// <see cref="Result{T}"/>), using cached reflection for the generic case. Mirrors
    /// <c>AuthorizationBehavior.CreateFailureResponse</c> so the cache-miss fallback never upcasts a
    /// non-generic <see cref="Result"/> to <c>Result&lt;T&gt;</c> (which would throw <see cref="InvalidCastException"/>).
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
    /// <summary>
    /// Handles a request by applying caching logic and invoking the next handler in the pipeline.
    /// </summary>
    /// <remarks>This method applies caching logic based on the presence of a <see
    /// cref="CachingPolicyAttribute"/> on the request type and the associated caching configuration. If caching is
    /// disabled or no caching policy is defined, the request is passed directly to the next handler without caching. If
    /// caching is enabled: <list type="bullet"> <item><description>The method attempts to retrieve a cached response
    /// using a generated cache key.</description></item> <item><description>If a cached response is found, it is
    /// returned immediately, bypassing further processing.</description></item> <item><description>If no cached
    /// response is found, the request is processed by the next handler, and the response is
    /// cached.</description></item> </list> The cache key can include user-specific information if per-user caching is
    /// enabled. Cache entries can be configured with absolute and sliding expiration policies.</remarks>
    /// <param name="request">The request object to be processed. Cannot be <see langword="null"/>.</param>
    /// <param name="next">A delegate representing the next handler in the pipeline. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the response of
    /// type <typeparamref name="TResponse"/>. If caching is enabled and a cached response is available, the cached
    /// response is returned; otherwise, the response is generated by the next handler and cached for future use.</returns>
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        Func<ValueTask<TResponse>> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var requestName = typeof(TRequest).FullName ?? string.Empty;

        // Is the request decorated with CachingPolicyAttribute?
        // If not,...
        if (request.GetType().GetCustomAttribute<CachingPolicyAttribute>() is null)
        {
            // Log that caching is not applied
            logger.LogDebug("Caching is not applied for request [{RequestName}]. Proceeding without caching.", requestName);

            // Continue processing the request without caching
            return await next.WithCancellation(cancellationToken, requestName, logger).ConfigureAwait(false);
        }

        // Is caching disabled in the configuration?
        var settings = CachingPolicySettings.Default;
        configuration.Bind($"MediatorSettings:{requestName}:CacheSettings", settings);

        // Yes, so proceed without caching
        if (settings.Disabled)
        {
            logger.LogDebug("Caching has been disabled for request [{RequestName}]. Proceeding without caching.",
                requestName);
            return await next.WithCancellation(cancellationToken, requestName, logger).ConfigureAwait(false);
        }

        // Caching is enabled...
        var cacheKey = request.GetCacheKey<TRequest, TResponse>();

        // If per user caching, add the user id to the caching key.
        if (settings.PerUser)
        {
            var userId = currentUserService.UserId;

            // Fail closed: an anonymous (null/empty UserId) caller under PerUser caching must NOT
            // collapse to an empty prefix, which would (a) make every anonymous caller share one
            // slot and (b) collide with the global non-PerUser key for the same request type.
            // Bypass the cache entirely for anonymous callers — run the handler without reading or
            // writing the cache.
            if (string.IsNullOrEmpty(userId))
            {
                logger.LogDebug(
                    "Per-user caching is enabled for request [{RequestName}] but the caller is anonymous (no UserId). Bypassing cache.",
                    requestName);
                return await next.WithCancellation(cancellationToken, requestName, logger).ConfigureAwait(false);
            }

            cacheKey = $"{userId}{cacheKey}";
        }

        // Cache reset requested?
        if (settings.Reset)
        {
            // Yes, log removal request, and...
            logger.LogDebug("Removing cached response for request [{RequestName}] with cache key [{CacheKey}].",
                requestName, cacheKey);

            // Remove the entry
            await cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // No, log the retrieval request, and...
            logger.LogDebug(
                "Attempting to retrieve cached response for request [{RequestName}] with cache key [{CacheKey}].",
                requestName, cacheKey);

            // Try to find the entry.
            var cachedResponse = await cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            // Found?
            if (cachedResponse != null)
            {
                // Log it, and...
                logger.LogDebug("Retrieved cached response for request [{RequestName}] with cache key [{CacheKey}].",
                    requestName, cacheKey);

                // Return the cached response, interrupting the pipeline.
                // On a corrupt or type-incompatible cache entry, fall through to the handler
                // rather than crashing. The deserializer can throw JsonException for malformed
                // JSON, but also NotSupportedException (a type STJ cannot construct) and
                // InvalidCastException (a value-shape mismatch) — all three are treated as
                // "corrupt entry" so any poisoned slot degrades to a fresh handler call.
                try
                {
                    return JsonSerializer.Deserialize<TResponse>(cachedResponse, s_jsonOptions)
                           ?? CreateFailureResponse(Error.InternalServerError("Error.InternalServerError",
                               $"Failed to deserialize cached response: [{cachedResponse}]."));
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidCastException or InvalidOperationException)
                {
                    logger.LogWarning(ex,
                        "Cached response for [{RequestName}] key [{CacheKey}] is corrupt and could not be deserialized. Falling through to handler.",
                        requestName, cacheKey);
                }
            }
        }

        // Pass the request to the handler
        var response = await next.WithCancellation(cancellationToken, requestName, logger).ConfigureAwait(false);

        // Only successes are cached. A failure is often transient (a timed-out dependency, say), and caching
        // it would keep serving the error after its cause has cleared.
        if (response.IsFailure)
        {
            logger.LogDebug("Not caching failed response for request [{RequestName}] with cache key [{CacheKey}].",
                requestName, cacheKey);
            return response;
        }

        // Log setting the cache
        logger.LogDebug("Caching response for request [{RequestName}] with cache key [{CacheKey}].", requestName, cacheKey);
        // Cache the response
        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response, s_jsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = settings.AbsoluteExpirationRelativeToNow,
                SlidingExpiration = settings.SlidingExpiration
            },
            cancellationToken).ConfigureAwait(false);

        // Return it to the caller
        return response;
    }
}
