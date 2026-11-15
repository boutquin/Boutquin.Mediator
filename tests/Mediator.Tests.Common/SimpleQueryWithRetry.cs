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

using Abstractions;
using Boutquin.Domain.Abstractions;
using Microsoft.Extensions.Logging;

public sealed class SimpleQueryWithRetry
{
    public sealed record Query(string Input) : IQuery<Result<Output>>;

    public sealed class Handler(ILogger<Query> logger) : IQueryHandler<Query, Result<Output>>
    {
        private static int s_numTries;

        /// <summary>Atomically resets the call counter; call this in test Arrange before each test.</summary>
        public static void Reset() => Interlocked.Exchange(ref s_numTries, 0);

        public ValueTask<Result<Output>> HandleAsync(Query request, CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Query received: [{RequestMessage}], expected to fail: [False].", request.Input);
            var tries = Interlocked.Increment(ref s_numTries);
            // For exception test: first call throws, second call succeeds
            if (request.Input == "EXCEPTION")
            {
                if (tries == 1)
                {
                    logger.LogError("Simulated exception on attempt {NumTries}.", tries);
                    throw new InvalidOperationException("Simulated failure");
                }
                logger.LogInformation("Query executed successfully.");
                return ValueTask.FromResult(Result.Success(new Output($"Processed message: [{request.Input}].")));
            }
            // For normal retry: first call fails, second call succeeds
            if (tries == 1)
            {
                logger.LogError("Transient failure on attempt {NumTries}.", tries);
                return ValueTask.FromResult(Result.Failure<Output>(Error.BadRequest("BadRequest.QueryFailed", "fail")));
            }
            logger.LogInformation("Query executed successfully.");
            return ValueTask.FromResult(Result.Success(new Output($"Processed message: [{request.Input}].")));
        }
    }

    public sealed record Output(string Reply);
}
