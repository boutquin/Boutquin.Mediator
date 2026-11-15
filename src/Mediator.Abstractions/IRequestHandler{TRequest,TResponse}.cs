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
/// Defines a contract for handling requests of type <typeparamref name="TRequest"/> and producing a response of type
/// <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>Implementations of this interface are responsible for processing requests of type <typeparamref
/// name="TRequest"/> and returning a result of type <typeparamref name="TResponse"/>. This interface supports
/// asynchronous operations and allows cancellation through a <see cref="CancellationToken"/>.</remarks>
/// <typeparam name="TRequest">The type of the request to be handled. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by handling the request. Must derive from <see cref="Result"/>.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : Result
{
    /// <summary>
    /// Handles the specified request asynchronously and returns the result.
    /// </summary>
    /// <remarks>This method processes the given request and produces a result of type <typeparamref
    /// name="TResponse"/>.  The caller can use the <paramref name="cancellationToken"/> to cancel the operation if
    /// needed.</remarks>
    /// <param name="request">The request to be handled. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation will terminate early if the token is canceled.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the outcome of
    /// processing the request.</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
