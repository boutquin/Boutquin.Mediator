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

using System.Text.Json;
using System.Text.Json.Serialization;

using Boutquin.Domain.Abstractions;

namespace Boutquin.Mediator.Caching;

/// <summary>
/// A <see cref="JsonConverterFactory"/> that handles serialization and deserialization of
/// <see cref="Result{TValue}"/> instances whose constructor is not publicly accessible.
/// </summary>
internal sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert == typeof(Result)
           || (typeToConvert.IsGenericType
               && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>));

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(Result))
        {
            return new NonGenericResultJsonConverter();
        }

        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ResultJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    /// <summary>
    /// Serializes and deserializes the non-generic <see cref="Result"/> by reading the
    /// <c>IsSuccess</c> and <c>Error</c> properties and reconstructing the result via
    /// <see cref="Result.Success()"/> / <see cref="Result.Failure(Error)"/> factory methods.
    /// The non-generic <see cref="Result"/> has no public parameterless constructor, so STJ's
    /// default object converter cannot read it; this converter is required to round-trip it.
    /// </summary>
    private sealed class NonGenericResultJsonConverter : JsonConverter<Result>
    {
        public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("IsSuccess", out var isSuccessProp))
            {
                throw new JsonException("Missing required property 'IsSuccess'.");
            }

            if (isSuccessProp.GetBoolean())
            {
                return Result.Success();
            }

            var error = root.TryGetProperty("Error", out var errorProp)
                ? JsonSerializer.Deserialize<Error>(errorProp.GetRawText(), options)
                : Error.None;

            return Result.Failure(error ?? Error.None);
        }

        public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteBoolean("IsSuccess", value.IsSuccess);

            if (value.IsFailure)
            {
                writer.WritePropertyName("Error");
                JsonSerializer.Serialize(writer, value.Error, options);
            }

            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// Serializes and deserializes <see cref="Result{TValue}"/> by reading the <c>IsSuccess</c>,
    /// <c>Error</c>, and <c>Value</c> properties and reconstructing the result via factory methods.
    /// </summary>
    private sealed class ResultJsonConverter<TValue> : JsonConverter<Result<TValue>>
    {
        public override Result<TValue>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("IsSuccess", out var isSuccessProp))
            {
                throw new JsonException("Missing required property 'IsSuccess'.");
            }

            var isSuccess = isSuccessProp.GetBoolean();

            if (isSuccess)
            {
                var value = root.TryGetProperty("Value", out var valueProp)
                    ? JsonSerializer.Deserialize<TValue>(valueProp.GetRawText(), options)
                    : default;

                return Result.Success(value!);
            }

            var error = root.TryGetProperty("Error", out var errorProp)
                ? JsonSerializer.Deserialize<Error>(errorProp.GetRawText(), options)
                : Error.None;

            return Result.Failure<TValue>(error ?? Error.None);
        }

        public override void Write(Utf8JsonWriter writer, Result<TValue> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteBoolean("IsSuccess", value.IsSuccess);

            if (value.IsFailure)
            {
                writer.WritePropertyName("Error");
                JsonSerializer.Serialize(writer, value.Error, options);
            }

            if (value.IsSuccess)
            {
                writer.WritePropertyName("Value");
                JsonSerializer.Serialize(writer, value.Value, options);
            }

            writer.WriteEndObject();
        }
    }
}
