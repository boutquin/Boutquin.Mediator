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
/// Defines a mechanism for processing a request before it is handled by a request handler.
/// </summary>
/// <remarks>Implement this interface to perform pre-processing logic, such as validation, logging, or enrichment,
/// before the main request handler processes the request.</remarks>
/// <typeparam name="TRequest">The type of the request to be processed. Must be a non-nullable type.</typeparam>
public interface IRequestPreProcessor<in TRequest> where TRequest : notnull
{
    /// <summary>
    /// Processes the specified request asynchronously and returns the result.
    /// This method is called before the request handler processes the request.
    /// </summary>
    /// <remarks>This method performs the processing of the request in an asynchronous manner. The caller can
    /// use the returned <see cref="ValueTask{TResult}"/> to await the completion of the operation or retrieve the
    /// result.</remarks>
    /// <param name="request">The request to be processed. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the outcome of
    /// the processing.</returns>
    ValueTask<Result> ProcessAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}
