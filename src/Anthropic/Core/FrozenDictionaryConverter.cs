using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anthropic.Core;

/// <summary>
/// Serializes <see cref="FrozenDictionary{TKey, TValue}"/> through its mutable dictionary shape.
///
/// <para>The SDK registers the closed instantiations it uses in <see cref="ModelBase"/> instead of a
/// <see cref="JsonConverterFactory"/>: creating the converter with
/// <see cref="Type.MakeGenericType(Type[])"/> needs runtime code generation, which Native AOT does not
/// support.</para>
/// </summary>
sealed class FrozenDictionaryConverter<TKey, TValue> : JsonConverter<FrozenDictionary<TKey, TValue>>
    where TKey : notnull
{
    public override FrozenDictionary<TKey, TValue>? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var dictionary = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options);
        if (dictionary == null)
        {
            return null;
        }

        return FrozenDictionary.ToFrozenDictionary(dictionary);
    }

    public override void Write(
        Utf8JsonWriter writer,
        FrozenDictionary<TKey, TValue> value,
        JsonSerializerOptions options
    )
    {
        JsonSerializer.Serialize(writer, value, typeof(IReadOnlyDictionary<TKey, TValue>), options);
    }
}
