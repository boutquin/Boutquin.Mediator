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

using System.Threading;
using System.Threading.Tasks;
using Boutquin.Domain.Abstractions;

/// <summary>
/// Defines a contract for sending requests and receiving responses asynchronously.
/// </summary>
/// <remarks>This interface is typically implemented by classes that handle the dispatching of requests to their
/// corresponding handlers and return the resulting response. It supports generic request and response types, enabling
/// flexible and type-safe communication patterns.</remarks>
public interface ISender
{
    /// <summary>
    /// Sends a request asynchronously and returns the corresponding response.
    /// </summary>
    /// <remarks>This method is designed to handle requests and responses in a strongly-typed manner. Ensure
    /// that the request and response types are compatible.</remarks>
    /// <typeparam name="TRequest">The type of the request to be sent. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The type of the response expected. Must inherit from <see cref="Result"/>.</typeparam>
    /// <param name="request">The request object to be sent. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the response of
    /// type <typeparamref name="TResponse"/>.</returns>
    ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
        where TResponse : Result;
}
