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

namespace Boutquin.Mediator.Abstractions;

using System.Collections.Concurrent;
using System.Reflection;
using Boutquin.Domain.Abstractions;

/// <summary>
/// Provides extension methods for <see cref="IRequest{TResponse}"/> instances.
/// </summary>
public static class RequestExtensions
{
    /// <summary>
    /// Cache of public readable non-indexer properties, keyed by runtime type.
    /// Avoids repeated <see cref="Type.GetProperties()"/> allocations on the hot path.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_propertyCache = new();

    /// <summary>
    /// Returns the fully qualified type name of the request.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The type of the response. Must inherit from <see cref="Result"/>.</typeparam>
    /// <param name="request">The request instance whose type name is returned.</param>
    /// <returns>
    /// The fully qualified name of the runtime type of <paramref name="request"/>,
    /// equivalent to <c>request.GetType().FullName</c>.
    /// </returns>
    public static string FullName<TRequest, TResponse>(this TRequest request)
        where TRequest : IRequest<TResponse>
        where TResponse : Result
            => request.GetType().FullName ?? string.Empty;

    /// <summary>
    /// Generates a cache key for the request by combining an optional prefix, the fully qualified request type name,
    /// and the serialized values of all public properties.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The type of the response. Must inherit from <see cref="Result"/>.</typeparam>
    /// <param name="request">The request instance for which the cache key is generated.</param>
    /// <param name="prefix">
    /// An optional prefix prepended to the cache key. The value is trimmed and converted to uppercase before use.
    /// If <see langword="null"/>, empty, or whitespace, no prefix is added.
    /// </param>
    /// <returns>
    /// A pipe-delimited string of the form <c>{PREFIX}|{FullName}|$Prop1:[Value1]|$Prop2:[Value2]|…</c>
    /// that uniquely identifies the request type and its current property values.
    /// </returns>
    public static string GetCacheKey<TRequest, TResponse>(this TRequest request, string? prefix = null)
        where TRequest : IRequest<TResponse>
        where TResponse : Result
    {
        prefix =
            string.IsNullOrWhiteSpace(prefix) ?
                string.Empty :
                prefix.Trim().ToUpperInvariant();

        var props = s_propertyCache.GetOrAdd(
                request.GetType(),
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                      .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                      .ToArray())
            .Select(pi => $"${pi.Name}:[{pi.GetValue(request, null)}]");

        return $"{prefix}|{request.FullName<TRequest, TResponse>()}|{string.Join("|", props)}";
    }
}
