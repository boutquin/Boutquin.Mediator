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

using Boutquin.Mediator.Pipeline;

// A simple request with properties for characterization
internal sealed class NamedRequest
{
    public string Name { get; init; } = string.Empty;
    public int Value { get; init; }
}

/// <summary>
/// Characterization tests for CR-13: PropertyInfo[] cache in LoggingPreProcessor.
/// Verifies that invoking the pre-processor multiple times on the same request type
/// produces consistent (non-corrupted) results, confirming the per-type cache is safe.
/// </summary>
public class LoggingPreProcessorCacheTests
{
    /// <summary>
    /// CR-13 characterization: calling ProcessAsync twice on the same type succeeds both times,
    /// proving the cached PropertyInfo[] is not corrupted across invocations.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CalledTwiceOnSameType_BothSucceed()
    {
        // Arrange
        var logger = new Mock<ILogger<NamedRequest>>();
        var processor = new LoggingPreProcessor<NamedRequest>(logger.Object);
        var request = new NamedRequest { Name = "Test", Value = 42 };

        // Act — invoke twice to exercise the cache path on the second call
        var result1 = await processor.ProcessAsync(request).ConfigureAwait(true);
        var result2 = await processor.ProcessAsync(request).ConfigureAwait(true);

        // Assert — both must succeed; the cache must not corrupt the result
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    /// <summary>
    /// CR-13 characterization: calling ProcessAsync on two different request types succeeds
    /// for both, confirming the cache discriminates correctly by type key.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_CalledOnDifferentTypes_BothSucceed()
    {
        // Arrange
        var logger1 = new Mock<ILogger<NamedRequest>>();
        var logger2 = new Mock<ILogger<IntValueRequest>>();
        var processor1 = new LoggingPreProcessor<NamedRequest>(logger1.Object);
        var processor2 = new LoggingPreProcessor<IntValueRequest>(logger2.Object);

        // Act
        var result1 = await processor1.ProcessAsync(new NamedRequest { Name = "A", Value = 1 }).ConfigureAwait(true);
        var result2 = await processor2.ProcessAsync(new IntValueRequest()).ConfigureAwait(true);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }
}
