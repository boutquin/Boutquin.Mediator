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

namespace Boutquin.Mediator.Pipeline;

using System.Collections.Concurrent;
using System.Reflection;
// Boutquin.Mediator
using Abstractions;
using Boutquin.Domain.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// A pre-processor that logs information about incoming requests of type <typeparamref name="TRequest"/>.
/// </summary>
/// <remarks>This pre-processor logs the type of the request and the values of its public, readable properties. It
/// is designed to be used in a pipeline where logging request details is necessary for debugging or auditing
/// purposes.</remarks>
/// <typeparam name="TRequest">The type of the request being processed. Must be a non-nullable type.</typeparam>
/// <param name="logger">The logger used to record request type and property values. Cannot be <see langword="null"/>.</param>
public sealed class LoggingPreProcessor<TRequest>(
    ILogger<TRequest> logger)
    : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_propertyCache = new();
    /// <summary>
    /// Processes the specified request asynchronously and logs its properties.
    /// </summary>
    /// <remarks>This method logs the type and public readable properties of the provided request object. 
    /// Ensure that the <paramref name="request"/> parameter is not null and has properties that can be read.</remarks>
    /// <param name="request">The request to process. Must be an instance of a type with readable public properties.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains a <see
    /// cref="Result"/> indicating the success of the operation.</returns>
    public ValueTask<Result> ProcessAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        // Log the type of the request
        logger.LogInformation("Processing request of type [{RequestType}].", typeof(TRequest).FullName);

        // Log the properties of the request (PropertyInfo[] is cached per type to avoid repeated reflection)
        var props = s_propertyCache.GetOrAdd(
            request.GetType(),
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                  .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                  .ToArray());
        foreach (var prop in props)
        {
            logger.LogInformation("\t{PropertyName}: [{PropertyValue}]", prop.Name, prop.GetValue(request));
        }

        // Return a successful result
        return ValueTask.FromResult(Result.Success());
    }
}
