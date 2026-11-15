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
/// Defines a behavior in a processing pipeline for handling requests and responses.
/// </summary>
/// <typeparam name="TRequest">The type of the request being processed. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the pipeline. Must inherit from <see cref="Result"/>.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    /// <summary>
    /// Handles the specified request and processes it through the provided pipeline delegate.
    /// </summary>
    /// <remarks>This method is typically used in a middleware or pipeline pattern, where the request is
    /// processed  by multiple components in sequence. Implementations should ensure that <paramref name="next"/> is 
    /// invoked to allow the pipeline to continue.</remarks>
    /// <param name="request">The request object to be processed. Must not be null.</param>
    /// <param name="next">A delegate representing the next step in the processing pipeline.  This delegate must be invoked to continue the
    /// pipeline execution.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResponse}"/> representing the asynchronous operation,  containing the response produced
    /// by the pipeline.</returns>
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        Func<ValueTask<TResponse>> next,
        CancellationToken cancellationToken = default
        );
}
