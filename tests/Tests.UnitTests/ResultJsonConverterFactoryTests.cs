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

using System.Text.Json;

using Boutquin.Domain.Abstractions;
using Boutquin.Mediator.Caching;

/// <summary>
/// Unit tests for <see cref="ResultJsonConverterFactory"/> serialization and deserialization
/// of <see cref="Result{TValue}"/> instances.
/// </summary>
/// <remarks>
/// Covers CR-18: success round-trip, failure round-trip, null-value success round-trip,
/// and <c>JsonConverterFactory.CanConvert</c> discrimination.
/// </remarks>
public sealed class ResultJsonConverterFactoryTests
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        Converters = { new ResultJsonConverterFactory() }
    };

    // -----------------------------------------------------------------------
    // CR-18: Result<string> success round-trip
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a successful <see cref="Result{TValue}"/> of <see cref="string"/> survives
    /// a serialize → deserialize round-trip with <c>IsSuccess = true</c> and the original value intact.
    /// </summary>
    [Fact]
    public void RoundTrip_SuccessResult_PreservesIsSuccessAndValue()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        const string expectedValue = "hello world";
        var original = Result.Success(expectedValue);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var json = JsonSerializer.Serialize(original, s_options);
        var deserialized = JsonSerializer.Deserialize<Result<string>>(json, s_options);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(deserialized);
        Assert.True(deserialized.IsSuccess, "Deserialized result must report IsSuccess = true.");
        Assert.Equal(expectedValue, deserialized.Value);
    }

    // -----------------------------------------------------------------------
    // CR-18: Result<string> failure round-trip
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a failed <see cref="Result{TValue}"/> of <see cref="string"/> survives
    /// a serialize → deserialize round-trip with <c>IsFailure = true</c> and the
    /// original <see cref="Error.Code"/> intact.
    /// </summary>
    [Fact]
    public void RoundTrip_FailureResult_PreservesIsFailureAndErrorCode()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var expectedError = Error.BadRequest("Cache.Miss", "No cached value found");
        var original = Result.Failure<string>(expectedError);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var json = JsonSerializer.Serialize(original, s_options);
        var deserialized = JsonSerializer.Deserialize<Result<string>>(json, s_options);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(deserialized);
        Assert.True(deserialized.IsFailure, "Deserialized result must report IsFailure = true.");
        Assert.NotNull(deserialized.Error);
        Assert.Equal(expectedError.Code, deserialized.Error.Code);
    }

    // -----------------------------------------------------------------------
    // Result<string?> null-value failure round-trip
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <see langword="null"/> value created through <see cref="Result.Create{TValue}(TValue)"/>
    /// survives a serialize → deserialize round-trip as the domain's null-value failure.
    /// </summary>
    [Fact]
    public void RoundTrip_NullValueCreatedResult_PreservesNullValueFailure()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var original = Result.Create<string?>(null);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var json = JsonSerializer.Serialize(original, s_options);
        var deserialized = JsonSerializer.Deserialize<Result<string?>>(json, s_options);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(deserialized);
        Assert.True(deserialized.IsFailure, "Deserialized result must report IsFailure = true.");
        Assert.Equal(Error.NullValue.Code, deserialized.Error.Code);
    }

    // -----------------------------------------------------------------------
    // CACHE-POISON-NONGENERIC-001: non-generic Result round-trip
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a successful non-generic <see cref="Result"/> survives a serialize →
    /// deserialize round-trip without throwing. The factory must register a converter for the
    /// non-generic <see cref="Result"/>; otherwise STJ falls back to its default object converter
    /// which cannot construct <see cref="Result"/> (no public parameterless constructor) and throws
    /// <see cref="NotSupportedException"/> on read.
    /// </summary>
    [Fact]
    public void RoundTrip_NonGenericSuccessResult_PreservesIsSuccess()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var original = Result.Success();

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var json = JsonSerializer.Serialize(original, s_options);
        var deserialized = JsonSerializer.Deserialize<Result>(json, s_options);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(deserialized);
        Assert.True(deserialized.IsSuccess, "Deserialized non-generic result must report IsSuccess = true.");
    }

    /// <summary>
    /// Verifies that a failed non-generic <see cref="Result"/> survives a serialize → deserialize
    /// round-trip with <c>IsFailure = true</c> and the original <see cref="Error.Code"/> intact.
    /// </summary>
    [Fact]
    public void RoundTrip_NonGenericFailureResult_PreservesIsFailureAndErrorCode()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var expectedError = Error.BadRequest("NonGeneric.Failure", "boom");
        var original = Result.Failure(expectedError);

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var json = JsonSerializer.Serialize(original, s_options);
        var deserialized = JsonSerializer.Deserialize<Result>(json, s_options);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(deserialized);
        Assert.True(deserialized.IsFailure, "Deserialized non-generic result must report IsFailure = true.");
        Assert.NotNull(deserialized.Error);
        Assert.Equal(expectedError.Code, deserialized.Error.Code);
    }

    /// <summary>
    /// Verifies that <see cref="ResultJsonConverterFactory.CanConvert"/> returns
    /// <see langword="true"/> for the non-generic <see cref="Result"/> type.
    /// </summary>
    [Fact]
    public void CanConvert_ReturnsTrueForNonGenericResult()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var factory = new ResultJsonConverterFactory();

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(factory.CanConvert(typeof(Result)), "CanConvert must return true for non-generic Result.");
    }

    // -----------------------------------------------------------------------
    // CR-18: CanConvert discrimination
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="ResultJsonConverterFactory.CanConvert"/> returns
    /// <see langword="true"/> for <c>Result&lt;string&gt;</c> and <see langword="false"/>
    /// for plain <see cref="string"/>.
    /// </summary>
    [Fact]
    public void CanConvert_ReturnsTrueForResultOfString_AndFalseForString()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var factory = new ResultJsonConverterFactory();

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(factory.CanConvert(typeof(Result<string>)), "CanConvert must return true for Result<string>.");
        Assert.False(factory.CanConvert(typeof(string)), "CanConvert must return false for plain string.");
    }

    // -----------------------------------------------------------------------
    // S2-02: Missing "IsSuccess" property → must throw JsonException
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that deserializing JSON that is missing the required <c>IsSuccess</c> property
    /// throws a <see cref="JsonException"/> rather than a <see cref="KeyNotFoundException"/>
    /// (which the old <c>GetProperty</c> call would have thrown).
    /// </summary>
    [Fact]
    public void Deserialize_MissingIsSuccessProperty_ThrowsJsonException()
    {
        //---------------------------------------------------------
        // Arrange — JSON object with NO IsSuccess field
        //---------------------------------------------------------
        const string Json = """{"Value":"hello"}""";
        var options = new JsonSerializerOptions { Converters = { new ResultJsonConverterFactory() } };

        //---------------------------------------------------------
        // Act & Assert
        //---------------------------------------------------------
        var ex = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Result<string>>(Json, options));

        Assert.Contains("IsSuccess", ex.Message);
    }

    // -----------------------------------------------------------------------
    // S5-02/S5-03: IsFailure not written; Error only written on failure
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that the serialized JSON for a success result does NOT contain an <c>IsFailure</c>
    /// property and does NOT contain an <c>Error</c> property.
    /// </summary>
    [Fact]
    public void Serialize_SuccessResult_DoesNotContainIsFailureOrError()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var result = Result.Success("hello");
        var options = new JsonSerializerOptions { Converters = { new ResultJsonConverterFactory() } };

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var json = JsonSerializer.Serialize(result, options);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.DoesNotContain("IsFailure", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"Error\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsSuccess", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Value\"", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that the serialized JSON for a failure result does NOT contain an <c>IsFailure</c>
    /// property, but DOES contain an <c>Error</c> property.
    /// </summary>
    [Fact]
    public void Serialize_FailureResult_ContainsErrorButNotIsFailure()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var result = Result.Failure<string>(Error.BadRequest("Test.Error", "something went wrong"));
        var options = new JsonSerializerOptions { Converters = { new ResultJsonConverterFactory() } };

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var json = JsonSerializer.Serialize(result, options);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.DoesNotContain("IsFailure", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Error\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsSuccess", json, StringComparison.OrdinalIgnoreCase);
    }
}
