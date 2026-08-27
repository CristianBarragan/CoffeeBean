using System.Text.Json;
using System.Text.Json.Serialization;

namespace Foundgine.Abstractions;

[JsonConverter(typeof(FieldIdJsonConverter))]
public readonly record struct FieldId(ushort Value);

/// <summary>
/// Lets <see cref="FieldId"/> be used both as an ordinary JSON value and as a
/// dictionary key (e.g. <c>IReadOnlyDictionary&lt;FieldId, object?&gt;</c>
/// results returned from mutation execution). System.Text.Json's default
/// object converter cannot serialize a struct as a property name unless it
/// explicitly overrides ReadAsPropertyName/WriteAsPropertyName, so without
/// this converter any dictionary keyed by FieldId throws NotSupportedException
/// the moment it is serialized.
/// </summary>
public sealed class FieldIdJsonConverter : JsonConverter<FieldId>
{
    public override FieldId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetUInt16());

    public override void Write(Utf8JsonWriter writer, FieldId value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);

    public override FieldId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(ushort.Parse(reader.GetString()!));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, FieldId value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString());
}