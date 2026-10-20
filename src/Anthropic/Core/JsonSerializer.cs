using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // The namespace is deliberate: generated code binds to this type.
namespace Anthropic;

/// <summary>
/// A thin wrapper over <see cref="System.Text.Json.JsonSerializer"/> that routes every call through
/// a <see cref="JsonTypeInfo"/> obtained from the supplied <see cref="JsonSerializerOptions"/>.
///
/// <para>Native AOT cannot use the reflection-based generic overloads of
/// <see cref="System.Text.Json.JsonSerializer"/>: they are annotated with
/// <c>RequiresUnreferencedCodeAttribute</c> and <c>RequiresDynamicCodeAttribute</c>
/// because the serializer inspects the type at runtime. The overloads that take a
/// <see cref="JsonTypeInfo"/> are statically analyzable instead. The source-generated
/// <see cref="AnthropicJsonSerializerContext"/> registers the SDK's own types, so the type info is
/// available without reflection.</para>
///
/// <para>This type deliberately lives in the <c>Anthropic</c> namespace so that generated code,
/// which imports <c>System.Text.Json</c>, binds these calls instead of the BCL overloads. It is
/// internal: user code keeps using <see cref="System.Text.Json.JsonSerializer"/>.</para>
/// </summary>
internal static class JsonSerializer
{
    /// <summary>
    /// The fallback resolver keeps JIT scenarios such as user-defined structured-output models
    /// working. Native AOT resolves the SDK's own types from the source-generated context and never
    /// needs it.
    /// </summary>
    static readonly JsonSerializerOptions DefaultOptions = CreateDefaultOptions();

    static readonly JsonSerializerOptions DefaultIndentedOptions = CreateDefaultIndentedOptions();

    [UnconditionalSuppressMessage(
        "AOT",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Used only as a JIT fallback; the source-generated context handles AOT."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Used only as a JIT fallback; the source-generated context handles AOT."
    )]
    static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                AnthropicJsonSerializerContext.Default,
                new DefaultJsonTypeInfoResolver()
            ),
        };
    }

    static JsonSerializerOptions CreateDefaultIndentedOptions()
    {
        var options = new JsonSerializerOptions(DefaultOptions) { WriteIndented = true };
        return options;
    }

    static JsonTypeInfo<TValue> GetTypeInfo<TValue>(JsonSerializerOptions? options)
    {
        options ??= DefaultOptions;
        return (JsonTypeInfo<TValue>)options.GetTypeInfo(typeof(TValue));
    }

    static JsonTypeInfo GetTypeInfo(JsonSerializerOptions? options, System.Type type)
    {
        options ??= DefaultOptions;
        return options.GetTypeInfo(type);
    }

    public static TValue? Deserialize<TValue>(JsonElement element, JsonSerializerOptions options) =>
        System.Text.Json.JsonSerializer.Deserialize(element, GetTypeInfo<TValue>(options));

    public static TValue? Deserialize<TValue>(JsonElement element) =>
        System.Text.Json.JsonSerializer.Deserialize(element, GetTypeInfo<TValue>(DefaultOptions));

    public static TValue? Deserialize<TValue>(JsonNode? node, JsonSerializerOptions options) =>
        System.Text.Json.JsonSerializer.Deserialize(node, GetTypeInfo<TValue>(options));

    public static TValue? Deserialize<TValue>(string json, JsonSerializerOptions options) =>
        System.Text.Json.JsonSerializer.Deserialize(json, GetTypeInfo<TValue>(options));

    public static TValue? Deserialize<TValue>(string json) =>
        System.Text.Json.JsonSerializer.Deserialize(json, GetTypeInfo<TValue>(DefaultOptions));

    public static TValue? Deserialize<TValue>(
        ref Utf8JsonReader reader,
        JsonSerializerOptions options
    ) => System.Text.Json.JsonSerializer.Deserialize(ref reader, GetTypeInfo<TValue>(options));

    public static ValueTask<TValue?> DeserializeAsync<TValue>(
        Stream utf8Json,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default
    ) =>
        System.Text.Json.JsonSerializer.DeserializeAsync(
            utf8Json,
            GetTypeInfo<TValue>(options),
            cancellationToken
        );

    public static string Serialize<TValue>(TValue value, JsonSerializerOptions options) =>
        System.Text.Json.JsonSerializer.Serialize(value, GetTypeInfo<TValue>(options));

    public static string Serialize<TValue>(TValue value, JsonTypeInfo jsonTypeInfo) =>
        System.Text.Json.JsonSerializer.Serialize(value, jsonTypeInfo);

    public static string Serialize(object? value, JsonTypeInfo jsonTypeInfo) =>
        System.Text.Json.JsonSerializer.Serialize(value, jsonTypeInfo);

    public static object? Deserialize(string json, JsonTypeInfo jsonTypeInfo) =>
        System.Text.Json.JsonSerializer.Deserialize(json, jsonTypeInfo);

    public static string Serialize<TValue>(TValue value) =>
        System.Text.Json.JsonSerializer.Serialize(value, GetTypeInfo<TValue>(DefaultOptions));

    public static string SerializeIndented<TValue>(TValue value) =>
        System.Text.Json.JsonSerializer.Serialize(
            value,
            GetTypeInfo<TValue>(DefaultIndentedOptions)
        );

    public static void Serialize<TValue>(
        Utf8JsonWriter writer,
        TValue value,
        JsonSerializerOptions options
    ) => System.Text.Json.JsonSerializer.Serialize(writer, value, GetTypeInfo<TValue>(options));

    public static string Serialize(
        object? value,
        System.Type inputType,
        JsonSerializerOptions options
    ) => System.Text.Json.JsonSerializer.Serialize(value, GetTypeInfo(options, inputType));

    public static void Serialize(
        Utf8JsonWriter writer,
        object? value,
        System.Type inputType,
        JsonSerializerOptions options
    ) => System.Text.Json.JsonSerializer.Serialize(writer, value, GetTypeInfo(options, inputType));

    public static JsonElement SerializeToElement<TValue>(
        TValue value,
        JsonSerializerOptions options
    ) => System.Text.Json.JsonSerializer.SerializeToElement(value, GetTypeInfo<TValue>(options));

    public static JsonElement SerializeToElement<TValue>(TValue value) =>
        System.Text.Json.JsonSerializer.SerializeToElement(
            value,
            GetTypeInfo<TValue>(DefaultOptions)
        );

    public static JsonElement SerializeToElement<TValue>(TValue value, JsonTypeInfo jsonTypeInfo) =>
        System.Text.Json.JsonSerializer.SerializeToElement(value, jsonTypeInfo);

    public static JsonNode? SerializeToNode<TValue>(TValue value, JsonSerializerOptions options) =>
        System.Text.Json.JsonSerializer.SerializeToNode(value, GetTypeInfo<TValue>(options));

    public static JsonNode? SerializeToNode<TValue>(TValue value) =>
        System.Text.Json.JsonSerializer.SerializeToNode(value, GetTypeInfo<TValue>(DefaultOptions));
}
