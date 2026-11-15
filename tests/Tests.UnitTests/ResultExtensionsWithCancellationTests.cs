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
using Boutquin.Mediator.Abstractions.Extensions;

/// <summary>
/// Unit tests for <see cref="ResultExtensions.WithCancellation{TResponse}"/>.
/// </summary>
/// <remarks>
/// Covers CANCEL-DOWNCAST-001: when the cancellation token is already signalled and
/// <c>TResponse</c> is a value-returning <see cref="Result{TValue}"/>, the cancelled branch
/// must construct a properly typed cancelled <see cref="Result{TValue}"/> rather than
/// downcasting a base <see cref="Result"/> (which throws <see cref="InvalidCastException"/>).
/// </remarks>
public sealed class ResultExtensionsWithCancellationTests
{
    // -----------------------------------------------------------------------
    // CANCEL-DOWNCAST-001: generic Result<T> response on the cancelled branch
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that an already-cancelled token produces a failed <see cref="Result{TValue}"/>
    /// (a cancelled result), NOT an <see cref="InvalidCastException"/>, when <c>TResponse</c>
    /// is closed over <see cref="Result{TValue}"/>.
    /// </summary>
    [Fact]
    public async Task WithCancellation_AlreadyCancelled_GenericResponse_ReturnsCancelledResultOfT()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var logger = new Mock<ILogger>().Object;
        Func<ValueTask<Result<int>>> next =
            () => new ValueTask<Result<int>>(Result.Success(42));

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await next.WithCancellation(cts.Token, "TestRequest", logger);

        //---------------------------------------------------------
        // Assert — the cancelled branch returned a failed Result<int>, not a throw
        //---------------------------------------------------------
        Assert.IsType<Result<int>>(result);
        Assert.True(result.IsFailure);
    }

    // -----------------------------------------------------------------------
    // CANCEL-DOWNCAST-001: non-generic base Result response still works
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that the non-generic base <see cref="Result"/> case continues to return a
    /// failed cancelled base <see cref="Result"/> on the cancelled branch.
    /// </summary>
    [Fact]
    public async Task WithCancellation_AlreadyCancelled_BaseResponse_ReturnsCancelledResult()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var logger = new Mock<ILogger>().Object;
        Func<ValueTask<Result>> next =
            () => new ValueTask<Result>(Result.Success());

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await next.WithCancellation(cts.Token, "TestRequest", logger);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsFailure);
    }

    // -----------------------------------------------------------------------
    // Guard polarity: not cancelled → next() result flows through unchanged
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when the token is not cancelled, the delegate's own result is returned
    /// unchanged (the success path is unaffected by the fix).
    /// </summary>
    [Fact]
    public async Task WithCancellation_NotCancelled_GenericResponse_ReturnsNextResult()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var logger = new Mock<ILogger>().Object;
        Func<ValueTask<Result<int>>> next =
            () => new ValueTask<Result<int>>(Result.Success(7));

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await next.WithCancellation(CancellationToken.None, "TestRequest", logger);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }
}
