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

namespace Boutquin.Tests.UnitTests;

using Boutquin.Domain.Abstractions;
using Boutquin.Mediator.Abstractions;
using Boutquin.Mediator.Helpers;
using Microsoft.Extensions.DependencyInjection;

// A request that returns Result<int>
internal sealed class IntValueRequest : IRequest<Result<int>>;

// A pre-processor that always returns a failure Result (non-generic)
internal sealed class FailingPreProcessor<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly Error _error;
    internal FailingPreProcessor(Error error) => _error = error;

    public ValueTask<Result> ProcessAsync(TRequest request, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result.Failure(_error));
}

// A handler that should not be called when the pre-processor fails
internal sealed class IntValueRequestHandler : IRequestHandler<IntValueRequest, Result<int>>
{
    public ValueTask<Result<int>> HandleAsync(IntValueRequest request, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result.Success(42));
}

/// <summary>
/// Tests for CR-12: pre-processor failure must produce a valid Result&lt;T&gt; failure
/// rather than throwing InvalidCastException.
/// </summary>
public class MediatorPreProcessorCastTests
{
    /// <summary>
    /// CR-12 TDD RED/GREEN: When a pre-processor returns Result.Failure and TResponse is Result&lt;T&gt;,
    /// the mediator must return a failure Result&lt;T&gt; — not throw InvalidCastException.
    /// </summary>
    [Fact]
    public async Task SendAsync_WhenPreProcessorFails_AndTResponseIsResultOfT_ReturnFailureResultOfT()
    {
        // Arrange
        var expectedError = Error.InternalServerError("PreProc.Failed", "Pre-processor vetoed the request.");

        var services = new ServiceCollection();
        services.AddSingleton<IRequestHandler<IntValueRequest, Result<int>>, IntValueRequestHandler>();
        services.AddSingleton<IRequestPreProcessor<IntValueRequest>>(new FailingPreProcessor<IntValueRequest>(expectedError));
        var sp = services.BuildServiceProvider();

        var mediator = new Mediator(sp);

        // Act
        // Before the CR-12 fix this throws InvalidCastException because
        // (TResponse)result tries to cast Result to Result<int>.
        var result = await mediator.SendAsync<IntValueRequest, Result<int>>(new IntValueRequest()).ConfigureAwait(true);

        // Assert — must be a failure carrying the same error; must NOT throw
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError.Code, result.Error.Code);
        Assert.Equal(expectedError.Description, result.Error.Description);
    }
}
