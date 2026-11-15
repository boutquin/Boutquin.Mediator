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
/// Defines a handler for processing commands of type <typeparamref name="TCommand"/>.
/// </summary>
/// <remarks>This interface extends <see cref="ICommandHandler{TCommand, TResult}"/> with a default result type of
/// <see cref="Result"/>. It is typically used when the command handler does not require a custom result type.</remarks>
/// <typeparam name="TCommand">The type of the command to be handled. Must implement the <see cref="ICommand"/> interface.</typeparam>
public interface ICommandHandler<in TCommand>
    : ICommandHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>
/// Defines a handler for processing commands of type <typeparamref name="TCommand"/>  and producing a result of type
/// <typeparamref name="TCommandResult"/>.
/// </summary>
/// <remarks>This interface extends <see cref="IRequestHandler{TRequest, TResponse}"/> to provide a  specialized
/// contract for handling commands within a command-processing pipeline. Implementations are expected to encapsulate the
/// logic required to process the command  and return an appropriate result.</remarks>
/// <typeparam name="TCommand">The type of the command to be handled. Must implement <see cref="ICommand{TCommandResult}"/>.</typeparam>
/// <typeparam name="TCommandResult">The type of the result produced by handling the command. Must derive from <see cref="Result"/>.</typeparam>
public interface ICommandHandler<in TCommand, TCommandResult>
    : IRequestHandler<TCommand, TCommandResult>
    where TCommand : ICommand<TCommandResult>
    where TCommandResult : Result;
