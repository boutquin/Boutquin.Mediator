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
/// A delegate representing the next handler in the streaming pipeline.
/// </summary>
/// <typeparam name="TResponse">The element type of each stream item.</typeparam>
/// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="Result{TValue}"/> items from the next stage of the pipeline.</returns>
public delegate IAsyncEnumerable<Result<TResponse>> StreamHandlerDelegate<TResponse>();

/// <summary>
/// Defines a streaming pipeline behavior that wraps a stream handler invocation.
/// </summary>
/// <remarks>Implement this interface to inspect or modify the item sequence produced by a stream request handler.
/// Streaming behaviors are applied in reverse registration order (outermost first), so the first registered
/// behavior is the outermost wrapper — it executes before all other behaviors and after all other behaviors
/// have completed.</remarks>
/// <typeparam name="TRequest">The type of the stream request. Must implement <see cref="IStreamRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The element type of each stream item.</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the stream request, optionally inspecting or modifying the item sequence produced by <paramref name="next"/>.
    /// </summary>
    /// <param name="request">The stream request to handle. Cannot be <see langword="null"/>.</param>
    /// <param name="next">A delegate that invokes the next stage of the streaming pipeline.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="Result{TValue}"/> items, potentially modified by this behavior.</returns>
    IAsyncEnumerable<Result<TResponse>> HandleAsync(TRequest request, StreamHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default);
}
