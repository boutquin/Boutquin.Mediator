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
/// Represents a query with disabled retry functionality, encapsulating a message and an optional failure flag.
/// </summary>
/// <remarks>This type is designed to handle queries where retry logic is explicitly disabled. It includes a
/// nested <see cref="Query"/> record for defining the query parameters and a <see cref="Handler"/> record for
/// processing the query asynchronously. The result of the query is encapsulated in a <see cref="Result{T}"/> object
/// containing an <see cref="Output"/>.</remarks>
public sealed record SimpleQueryWithDisabledRetry
{
    /// <summary>
    /// Represents a query with a message and an optional failure flag.
    /// </summary>
    /// <remarks>This query is used to encapsulate a message and an optional flag indicating whether the query
    /// should simulate a failure. It implements the <see cref="IQuery{TResult}"/> interface, where the result is of
    /// type <see cref="Result{T}"/> containing an <see cref="Output"/>.</remarks>
    /// <param name="Message">The message associated with the query. This value cannot be null.</param>
    /// <param name="Fail">A flag indicating whether the query should simulate a failure. Defaults to <see langword="false"/>.</param>
    [CachingPolicy]
    public sealed record Query(string Message, bool Fail = false) : IQuery<Result<Output>>;

    /// <summary>
    /// Represents a handler for processing <see cref="Query"/> requests asynchronously.
    /// </summary>
    /// <remarks>This handler processes incoming queries and returns a result indicating success or failure. 
    /// If the <see cref="Query.Fail"/> flag is set to <see langword="true"/>, the handler intentionally  returns a
    /// failure result with a <see cref="Error.BadRequest"/> error. Otherwise, it processes the  query and returns a
    /// successful result containing the processed message.</remarks>
    /// <param name="Logger"></param>
    public sealed record Handler(
        ILogger<Query> Logger) : IRequestHandler<Query, Result<Output>>
    {
        /// <summary>
        /// Handles the specified query asynchronously and returns the result.
        /// </summary>
        /// <remarks>The method processes the query based on the <see cref="Query.Fail"/> flag. If the
        /// flag is set to <see langword="true"/>,  the method intentionally returns a failure result with a
        /// "BadRequest.QueryFailed" error.</remarks>
        /// <param name="request">The query to process, containing the message and a flag indicating whether the query should fail.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> containing a <see cref="Result{T}"/> object that represents the outcome
        /// of the query. If the query succeeds, the result contains an <see cref="Output"/> object with the processed
        /// message. If the query fails, the result contains an error of type <see cref="Error.BadRequest"/>.</returns>
        public ValueTask<Result<Output>> HandleAsync(Query request, CancellationToken cancellationToken = default)
        {
            // Log the received query and whether it is expected to fail.
            Logger.LogDebug("Query received: [{RequestMessage}], expected to fail: [{RequestFail}].", request.Message, request.Fail);

            // Simulate processing the query.
            if (!request.Fail)
            {
                // If the query is not expected to fail, return a successful result.
                Logger.LogInformation("Query executed successfully.");
                return ValueTask.FromResult(Result.Success(new Output($"Processed message: [{request.Message}].")));
            }

            // If the query is expected to fail, return a failure result with a BadRequest error.
            Logger.LogError("Query failed intentionally with [Error.BadRequest].");
            return ValueTask.FromResult(Result.Failure<Output>(Error.BadRequest("BadRequest.QueryFailed", "The query failed intentionally with a [BadRequest.QueryFailed] Error.")));
        }
    }

    /// <summary>
    /// Represents the output of an operation, containing a reply message.
    /// </summary>
    /// <param name="Reply">The reply message associated with the output.</param>
    public sealed record Output(string Reply);
}
