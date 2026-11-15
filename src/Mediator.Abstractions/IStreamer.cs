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

using System.Collections.Generic;
using Boutquin.Domain.Abstractions;

/// <summary>
/// Defines a contract for dispatching stream requests that produce asynchronous sequences of results.
/// </summary>
/// <remarks>Implementations of this interface resolve the appropriate <see cref="IStreamRequestHandler{TRequest,TResponse}"/>
/// from the dependency injection container, apply any registered <see cref="IStreamPipelineBehavior{TRequest,TResponse}"/>
/// instances, and return the resulting <see cref="IAsyncEnumerable{T}"/> of <see cref="Result{TValue}"/> items.</remarks>
public interface IStreamer
{
    /// <summary>
    /// Dispatches a stream request to its registered handler and returns an asynchronous sequence of results.
    /// </summary>
    /// <typeparam name="TRequest">The type of the stream request. Must implement <see cref="IStreamRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The element type returned in each <see cref="Result{TValue}"/> item of the stream.</typeparam>
    /// <param name="request">The stream request to dispatch. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="Result{TValue}"/> items produced by the handler pipeline.</returns>
    IAsyncEnumerable<Result<TResponse>> CreateStream<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IStreamRequest<TResponse>;
}
