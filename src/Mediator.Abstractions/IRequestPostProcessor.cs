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

using Boutquin.Domain.Abstractions;

/// <summary>
/// Defines a post-processor that runs after a request handler completes.
/// </summary>
/// <remarks>Implement this interface to perform post-processing logic — such as logging, auditing, or
/// side-effect dispatch — after the main request handler has produced its response.</remarks>
/// <typeparam name="TRequest">The type of the request. Must be a non-nullable type.</typeparam>
/// <typeparam name="TResponse">The type of the response. Must inherit from <see cref="Result"/>.</typeparam>
public interface IRequestPostProcessor<in TRequest, in TResponse>
    where TResponse : Result
{
    /// <summary>
    /// Processes the request and its response after the handler has completed.
    /// </summary>
    /// <remarks>This method is called once the request handler has produced <paramref name="response"/>.
    /// Implementations should not mutate the response; use a pipeline behavior if transformation is required.</remarks>
    /// <param name="request">The request that was handled. Cannot be <see langword="null"/>.</param>
    /// <param name="response">The response returned by the request handler.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous post-processing operation. The result
    /// contains a <see cref="Result"/> indicating whether post-processing succeeded.</returns>
    ValueTask<Result> ProcessAsync(TRequest request, TResponse response, CancellationToken cancellationToken = default);
}
