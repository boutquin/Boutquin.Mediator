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
/// Represents a command that can be executed to perform an action or operation.
/// </summary>
/// <remarks>This interface serves as a base for commands that do not return a specific result.  It extends <see
/// cref="ICommand{TResult}"/> with a default result type.</remarks>
public interface ICommand
    : ICommand<Result>;

/// <summary>
/// Represents a command that produces a result of type <typeparamref name="TCommandResult"/>.
/// </summary>
/// <remarks>This interface extends <see cref="IRequest{TResponse}"/> to define a command pattern where the
/// execution of the command yields a result of the specified type.</remarks>
/// <typeparam name="TCommandResult">The type of the result produced by the command. Must derive from <see cref="Result"/>.</typeparam>
public interface ICommand<out TCommandResult>
    : IRequest<TCommandResult>
    where TCommandResult : Result;
