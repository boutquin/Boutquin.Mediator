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
/// Defines a mechanism for publishing notifications to registered handlers asynchronously.
/// </summary>
/// <remarks>Implementations of this interface are responsible for dispatching notifications to all appropriate
/// handlers. Notifications are typically used to signal events or changes within an application.</remarks>
public interface IPublisher
{
    /// <summary>
    /// Publishes the specified notification to all registered handlers asynchronously.
    /// </summary>
    /// <typeparam name="TNotification">The type of the notification to publish. Must implement <see cref="INotification"/>.</typeparam>
    /// <param name="notification">The notification instance to be published. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains a <see
    /// cref="Result"/>  indicating the success or failure of the publish operation.</returns>
    ValueTask<Result> PublishAsync<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default
    ) where TNotification : INotification;
}
