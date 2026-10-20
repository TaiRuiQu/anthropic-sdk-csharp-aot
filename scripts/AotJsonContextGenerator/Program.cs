using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.Core;

var assembly = typeof(ModelBase).Assembly;
var types = new SortedDictionary<string, Type>(StringComparer.Ordinal);

static string FormatType(Type type)
{
    if (type.IsByRef)
    {
        throw new NotSupportedException(type.ToString());
    }
    if (type.IsPointer || type.IsFunctionPointer || type.IsByRefLike)
    {
        throw new NotSupportedException(type.ToString());
    }
    if (type.IsArray)
    {
        return FormatType(type.GetElementType()!) + "[]";
    }
    if (Nullable.GetUnderlyingType(type) is Type underlying)
    {
        return FormatType(underlying) + "?";
    }
    if (type.IsGenericParameter)
    {
        throw new NotSupportedException(type.ToString());
    }
    if (type.IsGenericType)
    {
        var definition = type.GetGenericTypeDefinition();
        var name = (definition.FullName ?? definition.Name).Split('`')[0].Replace('+', '.');
        var arguments = type.GetGenericArguments().Select(FormatType);
        return $"global::{name}<{string.Join(", ", arguments)}>";
    }
    var fullName = type.FullName ?? type.Name;
    return "global::" + fullName.Replace('+', '.');
}

// Approximates the property name the System.Text.Json source generator derives from a type.
static string DefaultTypeInfoName(Type type)
{
    var builder = new StringBuilder();
    static void Append(Type current, StringBuilder target)
    {
        if (Nullable.GetUnderlyingType(current) is Type underlying)
        {
            Append(underlying, target);
            return;
        }
        if (current.IsArray)
        {
            Append(current.GetElementType()!, target);
            target.Append("Array");
            return;
        }
        if (current.IsGenericType)
        {
            target.Append(current.Name.Split('`')[0]);
            foreach (var argument in current.GetGenericArguments())
            {
                Append(argument, target);
            }
            return;
        }
        target.Append(current.Name);
    }
    Append(type, builder);
    return builder.ToString();
}

// Maps the CLR shape of a model property to the shape the raw-JSON dictionary accessors use.
static Type MapCollectionType(Type type)
{
    if (Nullable.GetUnderlyingType(type) is Type nullable)
    {
        return typeof(Nullable<>).MakeGenericType(MapCollectionType(nullable));
    }
    if (!type.IsGenericType)
    {
        return type;
    }

    var definition = type.GetGenericTypeDefinition();
    var arguments = type.GetGenericArguments();
    if (definition == typeof(IReadOnlyList<>))
    {
        return typeof(ImmutableArray<>).MakeGenericType(MapCollectionType(arguments[0]));
    }
    if (definition == typeof(IReadOnlyDictionary<,>))
    {
        return typeof(FrozenDictionary<,>).MakeGenericType(
            MapCollectionType(arguments[0]),
            MapCollectionType(arguments[1])
        );
    }
    return type;
}

void Add(Type type)
{
    try
    {
        if (
            type.IsGenericTypeDefinition
            || (
                type.IsAbstract
                && !type.IsInterface
                && !(
                    type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(FrozenDictionary<,>)
                )
            )
            || type.IsByRefLike
        )
        {
            return;
        }
        if (
            type.IsInterface
            && (
                !type.IsGenericType
                || (
                    type.GetGenericTypeDefinition() != typeof(IReadOnlyDictionary<,>)
                    && type.GetGenericTypeDefinition() != typeof(IReadOnlyList<>)
                )
            )
        )
        {
            return;
        }
        if (
            typeof(Delegate).IsAssignableFrom(type)
            || type == typeof(void)
            || type == typeof(Type)
            || type == typeof(object)
            || type == typeof(IntPtr)
            || type == typeof(UIntPtr)
        )
        {
            return;
        }
        if (type.FullName is null)
        {
            return;
        }

        var formatted = FormatType(type);
        if (!types.TryAdd(formatted, type))
        {
            return;
        }

        if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
        {
            Add(typeof(Nullable<>).MakeGenericType(type));
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                Add(argument);
            }
        }
    }
    catch (NotSupportedException) { }
}

bool IsModelType(Type type)
{
    if (type.Namespace is null)
    {
        return false;
    }
    if (
        type.Namespace != "Anthropic.Models"
        && !type.Namespace.StartsWith("Anthropic.Models.", StringComparison.Ordinal)
    )
    {
        return false;
    }
    return type.IsEnum || typeof(ModelBase).IsAssignableFrom(type);
}

foreach (var type in assembly.GetExportedTypes())
{
    if (!typeof(ParamsBase).IsAssignableFrom(type) || type.IsAbstract)
    {
        continue;
    }

    // Params objects are serialized through their raw-body dictionaries, so only the property
    // shapes their accessors use need metadata.
    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        Add(MapCollectionType(property.PropertyType));
    }
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
    {
        Add(field.FieldType);
    }
}

foreach (var type in assembly.GetExportedTypes())
{
    if (!IsModelType(type))
    {
        continue;
    }

    Add(type);

    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        Add(MapCollectionType(property.PropertyType));
    }
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
    {
        Add(field.FieldType);
    }
}

// Types that the SDK serializes but that aren't part of the exported model surface.
foreach (
    var name in new[]
    {
        "Anthropic.Credentials.ConfigFile",
        "Anthropic.Credentials.AuthenticationConfig",
        "Anthropic.Credentials.IdentityTokenConfig",
        "Anthropic.Credentials.CredentialsFileData",
    }
)
{
    var type = assembly.GetType(name, throwOnError: true)!;
    Add(type);
    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        Add(MapCollectionType(property.PropertyType));
    }
}

// Extra types recorded by a diagnostic run (one assembly-qualified name per line).
if (args.Length > 1 && File.Exists(args[1]))
{
    foreach (var line in File.ReadLines(args[1]))
    {
        var type = Type.GetType(line, throwOnError: false);
        if (type is null || type.Assembly.GetName().Name == "Anthropic.Tests")
        {
            continue;
        }

        Add(type);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Add(MapCollectionType(property.PropertyType));
        }
    }
}

// Shapes used only by specific generated converters. These aren't reachable from the public
// property surface, so they are listed explicitly.
void AddNamed(string name)
{
    var type = Type.GetType(name, throwOnError: false) ?? assembly.GetType(name, false);
    if (type is not null)
    {
        Add(type);
    }
}

void AddApiEnum(string enumName)
{
    var enumType = assembly.GetType(enumName, throwOnError: true)!;
    Add(typeof(ApiEnum<,>).MakeGenericType(typeof(string), enumType));
}

void AddList(string elementName)
{
    var elementType =
        Type.GetType(elementName, throwOnError: false)
        ?? assembly.GetType(elementName, throwOnError: true)!;
    Add(typeof(List<>).MakeGenericType(elementType));
}

void AddReadOnlyList(string elementName)
{
    var elementType =
        Type.GetType(elementName, throwOnError: false)
        ?? assembly.GetType(elementName, throwOnError: true)!;
    Add(typeof(IReadOnlyList<>).MakeGenericType(elementType));
}

void AddImmutableArray(string elementName)
{
    var elementType =
        Type.GetType(elementName, throwOnError: false)
        ?? assembly.GetType(elementName, throwOnError: true)!;
    Add(typeof(ImmutableArray<>).MakeGenericType(elementType));
}

void AddDictionary(string valueName)
{
    var valueType =
        Type.GetType(valueName, throwOnError: false)
        ?? assembly.GetType(valueName, throwOnError: true)!;
    Add(typeof(FrozenDictionary<,>).MakeGenericType(typeof(string), valueType));
    Add(typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType));
    Add(typeof(IReadOnlyDictionary<,>).MakeGenericType(typeof(string), valueType));
}

void AddImmutableArrayApiEnum(string enumName)
{
    var enumType = assembly.GetType(enumName, throwOnError: true)!;
    var apiEnum = typeof(ApiEnum<,>).MakeGenericType(typeof(string), enumType);
    Add(typeof(ImmutableArray<>).MakeGenericType(apiEnum));
}

void AddListApiEnum(string enumName)
{
    var enumType = assembly.GetType(enumName, throwOnError: true)!;
    var apiEnum = typeof(ApiEnum<,>).MakeGenericType(typeof(string), enumType);
    Add(typeof(List<>).MakeGenericType(apiEnum));
}

AddNamed("Anthropic.Models.Beta.Messages.All");
AddNamed("Anthropic.Models.Beta.Messages.Default");
AddNamed(
    "Anthropic.Models.Beta.Organization.Workspaces.BetaDataResidencyCreateConfigAllowedInferenceGeosUnrestricted"
);
AddNamed(
    "Anthropic.Models.Beta.Organization.Workspaces.BetaDataResidencyUpdateConfigAllowedInferenceGeosUnrestricted"
);
AddNamed("Anthropic.Models.Beta.Organization.Workspaces.Unrestricted");
AddNamed(
    "Anthropic.Models.Organization.Workspaces.DataResidencyCreateConfigAllowedInferenceGeosUnrestricted"
);
AddNamed(
    "Anthropic.Models.Organization.Workspaces.DataResidencyUpdateConfigAllowedInferenceGeosUnrestricted"
);
AddNamed("Anthropic.Models.Organization.Workspaces.Unrestricted");

AddApiEnum("Anthropic.Models.Beta.Agents.BetaManagedAgentsEffortLevel");
AddApiEnum("Anthropic.Models.Beta.Agents.BetaManagedAgentsWebFetchUrlSourceShorthand");
AddApiEnum("Anthropic.Models.Beta.Organization.Workspaces.BetaAllowedInferenceGeo");
AddApiEnum("Anthropic.Models.ErrorType");
AddApiEnum("Anthropic.Models.Organization.Workspaces.AllowedInferenceGeo");
AddImmutableArrayApiEnum("Anthropic.Models.Beta.Organization.Workspaces.BetaAllowedInferenceGeo");
AddImmutableArrayApiEnum("Anthropic.Models.Organization.Workspaces.AllowedInferenceGeo");
AddListApiEnum("Anthropic.Models.Beta.Organization.Workspaces.BetaAllowedInferenceGeo");
AddListApiEnum("Anthropic.Models.Organization.Workspaces.AllowedInferenceGeo");

AddDictionary("Anthropic.Models.Beta.Messages.BetaMcpToolConfig");
AddDictionary("Anthropic.Models.Beta.UserProfiles.BetaUserProfileTrustGrant");
AddDictionary("System.String");

AddList("Anthropic.Models.Beta.Messages.BetaTextBlockParam");
AddList("Anthropic.Models.Messages.TextBlockParam");
AddList("Anthropic.Models.Beta.Messages.BetaContentBlockParam");
AddList("Anthropic.Models.Beta.Messages.BetaFallbackParam");
AddList("Anthropic.Models.Beta.Messages.BetaTextBlock");
AddList("Anthropic.Models.Messages.ContentBlockParam");
AddList("Anthropic.Models.Messages.Block");
AddList("Anthropic.Models.Messages.MessageContentBlockSourceContent");
AddList("Anthropic.Models.Messages.WebSearchResultBlock");
AddList("Anthropic.Models.Messages.WebSearchResultBlockParam");
AddList("Anthropic.Models.Beta.Messages.Block");
AddList("Anthropic.Models.Beta.Messages.MessageBetaContentBlockSourceContent");
AddList("Anthropic.Models.Beta.Messages.BetaWebSearchResultBlock");
AddList("Anthropic.Models.Beta.Messages.BetaWebSearchResultBlockParam");

AddReadOnlyList("Anthropic.Models.Beta.Agents.BetaManagedAgentsMcpServerUrlDefinition");
AddReadOnlyList("Anthropic.Models.Beta.Agents.BetaManagedAgentsWebFetchUrlSourceToolReference");
AddReadOnlyList("Anthropic.Models.Beta.Messages.BetaCodeExecutionOutputBlock");
AddReadOnlyList("Anthropic.Models.Beta.Messages.BetaCodeExecutionOutputBlockParam");
AddReadOnlyList("Anthropic.Models.Beta.Messages.BetaWebFetchUrlSourceToolReference");
AddReadOnlyList("Anthropic.Models.Messages.CodeExecutionOutputBlock");
AddReadOnlyList("Anthropic.Models.Messages.CodeExecutionOutputBlockParam");
AddReadOnlyList("Anthropic.Models.Messages.WebFetchUrlSourceToolReference");
AddReadOnlyList("System.String");
AddReadOnlyList("Anthropic.Models.Messages.ContentBlockParam");
AddReadOnlyList("Anthropic.Models.Beta.Messages.BetaContentBlockParam");

AddImmutableArray("Anthropic.Models.Beta.Messages.BetaContentBlockParam");
AddImmutableArray("Anthropic.Models.Beta.Messages.BetaFallbackParam");
AddImmutableArray("Anthropic.Models.Beta.Messages.BetaTextBlock");
AddImmutableArray("Anthropic.Models.Beta.Messages.BetaWebSearchResultBlock");
AddImmutableArray("Anthropic.Models.Beta.Messages.BetaWebSearchResultBlockParam");
AddImmutableArray("Anthropic.Models.Beta.Messages.Block");
AddImmutableArray("Anthropic.Models.Beta.Messages.MessageBetaContentBlockSourceContent");
AddImmutableArray("Anthropic.Models.Messages.Block");
AddImmutableArray("Anthropic.Models.Messages.ContentBlockParam");
AddImmutableArray("Anthropic.Models.Messages.MessageContentBlockSourceContent");
AddImmutableArray("Anthropic.Models.Messages.WebSearchResultBlock");
AddImmutableArray("Anthropic.Models.Messages.WebSearchResultBlockParam");

Add(typeof(Guid));

// Collection shapes used by the raw-JSON dictionary accessors and credential requests.
Add(typeof(JsonElement));
Add(typeof(JsonObject));
Add(typeof(JsonArray));
Add(typeof(JsonValue));
Add(typeof(Dictionary<string, JsonElement>));
Add(typeof(Dictionary<string, MultipartJsonElement>));
Add(typeof(Dictionary<string, string>));
Add(typeof(IReadOnlyDictionary<string, JsonElement>));
Add(typeof(IReadOnlyDictionary<string, MultipartJsonElement>));
Add(typeof(IReadOnlyDictionary<string, string>));
Add(typeof(IReadOnlyList<JsonElement>));
Add(typeof(List<JsonElement>));
Add(typeof(List<string>));
Add(typeof(ImmutableArray<string>));
Add(typeof(ImmutableArray<JsonElement>));
Add(typeof(FrozenDictionary<string, JsonElement>));
Add(typeof(FrozenDictionary<string, string>));
Add(typeof(FrozenDictionary<string, MultipartJsonElement>));

// Give colliding source-generated property names a unique suffix. The generator itself does not
// disambiguate types that share a simple name across namespaces.
var names = types.ToDictionary(
    entry => entry.Key,
    entry => DefaultTypeInfoName(entry.Value),
    StringComparer.Ordinal
);
var nameGroups = names.GroupBy(entry => entry.Value, StringComparer.Ordinal).ToList();

// The source generator looks up a converter named after the type-info property. That lookup only
// works for generated converters; built-in BCL types (bool, string, JsonElement, ...) keep their
// default names, and a colliding SDK type gets the suffix instead.
bool IsBuiltIn(Type type) => type.Assembly != assembly && !type.IsGenericType;

var typeInfoNames = new Dictionary<string, string?>(StringComparer.Ordinal);
var suffix = 0;
foreach (var group in nameGroups)
{
    var entries = group.ToList();
    if (
        entries.Count == 1
        && group.Key is not ("Default" or "Options" or "GeneratedSerializerOptions")
    )
    {
        typeInfoNames[entries[0].Key] = null;
        continue;
    }

    var builtIns = entries.Count(entry => IsBuiltIn(types[entry.Key]));
    if (builtIns == entries.Count)
    {
        throw new InvalidOperationException(
            $"All entries named '{group.Key}' are built-in BCL types; cannot disambiguate."
        );
    }

    foreach (var entry in entries)
    {
        typeInfoNames[entry.Key] = IsBuiltIn(types[entry.Key]) ? null : $"{group.Key}_{suffix++}";
    }
}

var builder = new StringBuilder();
builder.AppendLine("// <auto-generated>");
builder.AppendLine(
    "// Generated from the Anthropic assembly's model surface by scripts/AotJsonContextGenerator."
);
builder.AppendLine("// Regenerate after adding model types.");
builder.AppendLine("// </auto-generated>");
builder.AppendLine("using System.Text.Json.Serialization;");
builder.AppendLine();
builder.AppendLine("namespace Anthropic;");
builder.AppendLine();
builder.AppendLine(
    "[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]"
);
foreach (var (formatted, _) in types)
{
    var propertyName = typeInfoNames[formatted];
    if (propertyName is null)
    {
        builder.AppendLine($"[JsonSerializable(typeof({formatted}))]");
    }
    else
    {
        builder.AppendLine(
            $"[JsonSerializable(typeof({formatted}), TypeInfoPropertyName = \"{propertyName}\")]"
        );
    }
}
builder.AppendLine(
    "internal partial class AnthropicJsonSerializerContext : JsonSerializerContext;"
);

File.WriteAllText(args[0], builder.ToString());
Console.WriteLine($"Generated {types.Count} JsonSerializable entries.");
