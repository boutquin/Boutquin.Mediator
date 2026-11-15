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
using Caching;
using Microsoft.Extensions.Logging;

/// <summary>
/// A cacheable command whose handler returns the <b>non-generic</b> <see cref="Result"/>.
/// </summary>
/// <remarks>
/// Used to reproduce CACHE-POISON-NONGENERIC-001: a <see cref="CachingPolicyAttribute"/>-decorated
/// request whose response is the non-generic <see cref="Result"/>. The first miss serializes the
/// response into the cache; every subsequent hit must deserialize it back to a non-generic
/// <see cref="Result"/> without crashing the pipeline.
/// </remarks>
public sealed record SimpleCommandWithCaching
{
    /// <summary>
    /// A cacheable command returning the non-generic <see cref="Result"/>.
    /// </summary>
    /// <param name="Message">The message associated with the command.</param>
    [CachingPolicy]
    public sealed record Command(string Message) : ICommand;

    /// <summary>
    /// Handler for <see cref="Command"/> that returns a successful non-generic <see cref="Result"/>.
    /// </summary>
    /// <param name="Logger">The logger instance.</param>
    public sealed record Handler(
        ILogger<Command> Logger) : ICommandHandler<Command>
    {
        /// <summary>
        /// Handles the command and returns a successful non-generic <see cref="Result"/>.
        /// </summary>
        /// <param name="request">The command to process.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A successful non-generic <see cref="Result"/>.</returns>
        public ValueTask<Result> HandleAsync(Command request, CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Cacheable command received: [{RequestMessage}].", request.Message);
            return ValueTask.FromResult(Result.Success());
        }
    }
}
