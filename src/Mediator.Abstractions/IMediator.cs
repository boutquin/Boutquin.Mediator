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

/// <summary>
/// Defines a mediator that coordinates communication between objects by publishing notifications, sending requests,
/// and dispatching stream requests.
/// This interface combines the functionality of <see cref="IPublisher"/>, <see cref="ISender"/>, and <see cref="IStreamer"/>.
/// </summary>
/// <remarks>Implementations of <see cref="IMediator"/> are responsible for decoupling the communication between
/// objects, allowing for more modular and testable code. Use the <see cref="IPublisher"/> functionality to publish
/// notifications to multiple handlers, the <see cref="ISender"/> functionality to send requests to a single
/// handler, and the <see cref="IStreamer"/> functionality to dispatch stream requests that produce asynchronous
/// sequences of results.</remarks>
public interface IMediator : IPublisher, ISender, IStreamer;
