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

namespace Boutquin.Mediator.Tests.Common;

// Boutquin.Mediator
using Abstractions;
using Boutquin.Domain.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents a simple command pattern implementation with a command and its corresponding handler.
/// </summary>
/// <remarks>This type encapsulates a command and its handler, providing a structure for executing operations with
/// optional failure simulation. The command contains a message and a flag indicating whether the operation should fail.
/// The handler processes the command and logs the outcome.</remarks>
public sealed record SimpleCommand
{
    /// <summary>
    /// Represents a command with a message and an optional failure flag.
    /// </summary>
    /// <remarks>This type is immutable and can be used to encapsulate a message and a failure state for
    /// processing commands in a system. It implements the <see cref="ICommand"/> interface.</remarks>
    /// <param name="Message">The message associated with the command. This value cannot be null.</param>
    /// <param name="Fail">A boolean value indicating whether the command should be treated as a failure. Defaults to <see
    /// langword="false"/>.</param>
    public sealed record Command(string Message, bool Fail = false) : ICommand;

    /// <summary>
    /// Handles the execution of a <see cref="Command"/> and returns the result of the operation.
    /// </summary>
    /// <remarks>This handler processes a <see cref="Command"/> and logs the execution details.  If the
    /// command is marked to fail, it returns a <see cref="Result"/> with an error of type <see
    /// cref="Error.BadRequest"/>. Otherwise, it returns a successful <see cref="Result"/>.</remarks>
    /// <param name="Logger"></param>
    public sealed record Handler(
        ILogger<Command> Logger
        ) : ICommandHandler<Command>
    {
        /// <summary>
        /// Handles the specified command asynchronously and returns the result of the operation.
        /// </summary>
        /// <remarks>If the <paramref name="request"/> specifies that the operation should fail, the
        /// method returns an error result with a  <see cref="Error.BadRequest"/> indicating the failure
        /// reason.</remarks>
        /// <param name="request">The command to process, containing the message and a flag indicating whether the operation should fail.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> representing the result of the operation.  Returns <see
        /// cref="Result.Success"/> if the command is processed successfully, or an error result if the command fails.</returns>
        public ValueTask<Result> HandleAsync(Command request, CancellationToken cancellationToken = default)
        {
            // Log the command details
            Logger.LogDebug("Command received: [{RequestMessage}], expected to fail: [{RequestFail}].", request.Message, request.Fail);

            // Check if the command is marked to fail
            if (!request.Fail)
            {
                // If the command is not marked to fail, return a successful result
                Logger.LogInformation("Command executed successfully.");
                return ValueTask.FromResult(Result.Success());
            }

            // If the command is marked to fail, return an error result
            Logger.LogError("Command failed intentionally with [Error.BadRequest].");
            return ValueTask.FromResult<Result>(Error.BadRequest("BadRequest.CommandFailed", "The command failed intentionally with a [BadRequest.CommandFailed] Error."));
        }
    }
}
