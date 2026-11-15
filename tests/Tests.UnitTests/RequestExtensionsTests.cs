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

/// <summary>
/// Unit tests for <see cref="RequestExtensions.GetCacheKey{TRequest,TResponse}"/> and
/// <see cref="RequestExtensions.FullName{TRequest,TResponse}"/>.
/// </summary>
/// <remarks>
/// Covers CR-20: prefix handling (absent, present, whitespace-only), property inclusion in the
/// cache key, and the <c>FullName</c> fully-qualified-type-name helper.
/// </remarks>
public sealed class RequestExtensionsTests
{
    // -----------------------------------------------------------------------
    // Minimal test request type
    // -----------------------------------------------------------------------

    /// <summary>
    /// Minimal request fixture with two public properties so the cache key includes them.
    /// </summary>
    private sealed record SampleRequest(string Name, int Count) : IRequest<Result<string>>;

    // -----------------------------------------------------------------------
    // CR-20: no prefix → key starts with pipe character
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when no prefix is supplied, the cache key starts with a pipe character
    /// (<c>|</c>) followed by the fully-qualified type name.
    /// </summary>
    [Fact]
    public void GetCacheKey_NoPrefixSupplied_StartsWithPipe()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var request = new SampleRequest("alpha", 1);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var key = request.GetCacheKey<SampleRequest, Result<string>>();

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.StartsWith("|", key, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // CR-20: with prefix → key starts with uppercased prefix followed by pipe
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a supplied prefix is trimmed and uppercased before being prepended to
    /// the cache key, separated from the type name by a pipe character.
    /// </summary>
    [Fact]
    public void GetCacheKey_WithPrefix_StartsWithUppercasedPrefix()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var request = new SampleRequest("beta", 2);
        const string prefix = "user-abc";
        const string expectedPrefix = "USER-ABC";

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var key = request.GetCacheKey<SampleRequest, Result<string>>(prefix);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.StartsWith(expectedPrefix + "|", key, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // CR-20: whitespace-only prefix → treated as no prefix (key starts with |)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a prefix consisting entirely of whitespace is treated the same as
    /// no prefix, so the key still starts with a pipe character.
    /// </summary>
    [Fact]
    public void GetCacheKey_WhitespaceOnlyPrefix_TreatedAsNoPrefix()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var request = new SampleRequest("gamma", 3);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var keyWithWhitespace = request.GetCacheKey<SampleRequest, Result<string>>("   ");
        var keyWithNull = request.GetCacheKey<SampleRequest, Result<string>>(null);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.StartsWith("|", keyWithWhitespace, StringComparison.Ordinal);
        Assert.Equal(keyWithNull, keyWithWhitespace);
    }

    // -----------------------------------------------------------------------
    // CR-20: key includes all public property values
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that the cache key contains the values of all public properties of the
    /// request in the expected <c>$PropertyName:[Value]</c> format.
    /// </summary>
    [Fact]
    public void GetCacheKey_IncludesAllPublicPropertyValues()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var request = new SampleRequest("delta", 42);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var key = request.GetCacheKey<SampleRequest, Result<string>>();

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.Contains("$Name:[delta]", key, StringComparison.Ordinal);
        Assert.Contains("$Count:[42]", key, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // CR-20: FullName returns the fully-qualified type name
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="RequestExtensions.FullName{TRequest,TResponse}"/> returns
    /// the fully qualified name of the request's runtime type.
    /// </summary>
    [Fact]
    public void FullName_ReturnsFullyQualifiedTypeName()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var request = new SampleRequest("epsilon", 5);
        var expected = typeof(SampleRequest).FullName ?? string.Empty;

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var actual = request.FullName<SampleRequest, Result<string>>();

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.Equal(expected, actual);
        Assert.Contains("SampleRequest", actual, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // S7-02: Request with indexer property — must not throw TargetParameterCountException
    // -----------------------------------------------------------------------

    /// <summary>
    /// A request type that exposes an indexer (parameterized property). Without the
    /// <c>GetIndexParameters().Length == 0</c> filter, calling <c>GetValue(request, null)</c>
    /// on an indexer property throws <see cref="System.Reflection.TargetParameterCountException"/>.
    /// </summary>
    private sealed class RequestWithIndexer : IRequest<Result>
    {
        public string Name { get; } = "test";

        // Indexer — must be excluded from the cache-key enumeration
        public string this[int index] => index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Verifies that <see cref="RequestExtensions.GetCacheKey{TRequest,TResponse}"/> does not throw
    /// when the request type contains an indexer property.
    /// </summary>
    [Fact]
    public void GetCacheKey_RequestWithIndexer_DoesNotThrow()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var request = new RequestWithIndexer();

        //---------------------------------------------------------
        // Act — must not throw TargetParameterCountException
        //---------------------------------------------------------
        var key = request.GetCacheKey<RequestWithIndexer, Result>();

        //---------------------------------------------------------
        // Assert — key was produced and contains the non-indexer property
        //---------------------------------------------------------
        Assert.False(string.IsNullOrEmpty(key));
        Assert.Contains("$Name:[test]", key, StringComparison.Ordinal);
    }
}
