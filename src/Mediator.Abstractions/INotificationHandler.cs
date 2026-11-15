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
/// Defines a contract for handling notifications of a specified type.
/// </summary>
/// <remarks>Implementations of this interface are responsible for processing notifications of the specified type.
/// This is typically used in event-driven or messaging systems to handle specific events or messages.</remarks>
/// <typeparam name="TNotification">The type of notification to handle. Must implement the <see cref="INotification"/> interface.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the specified notification asynchronously and returns the result of the operation.
    /// </summary>
    /// <param name="notification">The notification to be processed. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains a <see
    /// cref="Result"/> object that indicates the outcome of the operation.</returns>
    ValueTask<Result> HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}
