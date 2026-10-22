// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// 把操作的 JSON Schema（draft 2020-12，<c>Kernel.Generators</c> 的产出形态）映射为 protobuf 消息描述。字段号按 <c>properties</c> 的声明顺序从 1 起分配。
/// 映射：<c>string</c> → string（<c>contentEncoding: base64</c> → bytes）；<c>boolean</c> → bool；<c>integer</c> 按 <c>format</c> int32 / int64，没有格式按十进制文本；
/// <c>number</c> 按 <c>format</c> float / double，decimal 与没有格式按十进制文本；<c>array</c> → repeated（元素不能再是数组或映射）；
/// 带 <c>additionalProperties</c> 的 <c>object</c> → map&lt;string, T&gt;；<c>$ref</c> → 嵌套消息（<c>$defs</c>）；可空（<c>type</c> 含 <c>null</c> 或 <c>anyOf</c> 含 null）→ optional；没有约束的 Schema → JSON 文本。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ProtoSchema
{
    /// <summary>
    /// <c>$defs</c> 引用的前缀。
    /// </summary>
    private const string DefinitionPrefix = "#/$defs/";

    /// <summary>
    /// 从 Schema 文本建根消息（名字取 <c>title</c>）；<c>$defs</c> 里的对象成为嵌套消息。不能映射时抛出 <see cref="ProtoException"/>。
    /// </summary>
    public static ProtoMessage FromJsonSchema(string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(schema);
        }
        catch (JsonException exception)
        {
            throw new ProtoException("The schema is not valid JSON.", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ProtoException("The schema must be a JSON object.");
            }

            // 先为每个定义建空消息再填字段：定义之间可以互相引用，甚至递归
            var definitions = new Dictionary<string, ProtoMessage>(StringComparer.Ordinal);
            var hasDefinitions = root.TryGetProperty("$defs", out var definitionElements) && definitionElements.ValueKind == JsonValueKind.Object;
            if (hasDefinitions)
            {
                foreach (var definition in definitionElements.EnumerateObject())
                {
                    definitions[definition.Name] = new ProtoMessage(definition.Name);
                }
            }

            var name = root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(title.GetString()) ? title.GetString()! : "Message";
            if (definitions.ContainsKey(name))
            {
                throw new ProtoException($"The root message '{name}' collides with a definition of the same name.");
            }

            var message = new ProtoMessage(name);
            if (hasDefinitions)
            {
                foreach (var definition in definitionElements.EnumerateObject())
                {
                    Fill(definitions[definition.Name], definition.Value, definitions);
                }
            }

            Fill(message, root, definitions);
            return message;
        }
    }

    /// <summary>
    /// 以对象 Schema 的 <c>properties</c> 填消息字段。
    /// </summary>
    private static void Fill(ProtoMessage message, JsonElement schema, IReadOnlyDictionary<string, ProtoMessage> definitions)
    {
        var (type, _, node) = Classify(schema, message.Name);
        if (type != "object" || !node.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            throw new ProtoException($"Schema '{message.Name}' must be an object with properties to become a protobuf message.");
        }

        var fields = new List<ProtoField>();
        var number = 1;
        foreach (var property in properties.EnumerateObject())
        {
            fields.Add(FieldOf(number++, property.Name, property.Value, definitions, message.Name + "." + property.Name));
        }

        message.Complete(fields);
    }

    /// <summary>
    /// 一个属性 → 一个字段：数组成重复字段，带 <c>additionalProperties</c> 的对象成映射字段，其余按可空性成 optional 或单值字段。
    /// </summary>
    private static ProtoField FieldOf(int number, string jsonName, JsonElement schema, IReadOnlyDictionary<string, ProtoMessage> definitions, string path)
    {
        var (type, nullable, node) = Classify(schema, path);
        switch (type)
        {
            case "array":
                if (!node.TryGetProperty("items", out var items))
                {
                    throw new ProtoException($"'{path}': an array schema needs 'items'.");
                }

                return new ProtoField(number, jsonName, ElementOf(items, definitions, path + "[]"), ProtoCardinality.Repeated);
            case "object" when node.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.Object:
                return new ProtoField(number, jsonName, ElementOf(additional, definitions, path + "{}"), ProtoCardinality.Map);
            default:
                return new ProtoField(number, jsonName, ValueOf(type, node, definitions, path), nullable ? ProtoCardinality.Optional : ProtoCardinality.Singular);
        }
    }

    /// <summary>
    /// 数组元素或映射值的类型：protobuf 没有嵌套的 repeated / map，这里只接受标量与消息。
    /// </summary>
    private static ProtoValueType ElementOf(JsonElement schema, IReadOnlyDictionary<string, ProtoMessage> definitions, string path)
    {
        var (type, _, node) = Classify(schema, path);
        if (type == "array")
        {
            throw new ProtoException($"'{path}': nested arrays are not representable in protobuf; wrap the inner array in a type.");
        }

        if (type == "object" && node.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.Object)
        {
            throw new ProtoException($"'{path}': maps inside arrays or maps are not representable in protobuf; wrap the map in a type.");
        }

        return ValueOf(type, node, definitions, path);
    }

    /// <summary>
    /// 标量或消息的类型。
    /// </summary>
    private static ProtoValueType ValueOf(string type, JsonElement node, IReadOnlyDictionary<string, ProtoMessage> definitions, string path)
    {
        switch (type)
        {
            case "ref":
                var reference = node.GetProperty("$ref").GetString() ?? string.Empty;
                if (!reference.StartsWith(DefinitionPrefix, StringComparison.Ordinal) || !definitions.TryGetValue(reference[DefinitionPrefix.Length..], out var message))
                {
                    throw new ProtoException($"'{path}': reference '{reference}' does not point to a definition in $defs.");
                }

                return ProtoValueType.Of(message);
            case "string":
                return ProtoValueType.Scalar(node.TryGetProperty("contentEncoding", out var encoding) && string.Equals(encoding.GetString(), "base64", StringComparison.Ordinal) ? ProtoKind.Bytes : ProtoKind.Text);
            case "boolean":
                return ProtoValueType.Scalar(ProtoKind.Bool);
            case "integer":
                return ProtoValueType.Scalar(FormatOf(node) switch
                {
                    "int32" => ProtoKind.Varint32,
                    "int64" => ProtoKind.Varint64,
                    _ => ProtoKind.TextNumber,
                });
            case "number":
                return ProtoValueType.Scalar(FormatOf(node) switch
                {
                    "float" => ProtoKind.Fixed32,
                    "double" => ProtoKind.Fixed64,
                    _ => ProtoKind.TextNumber,
                });
            case "object":
                if (node.TryGetProperty("properties", out _))
                {
                    throw new ProtoException($"'{path}': inline object schemas are not supported; the generator puts nested types in $defs.");
                }

                return ProtoValueType.Scalar(ProtoKind.Json);
            case "any":
            case "null":
                return ProtoValueType.Scalar(ProtoKind.Json);
            default:
                throw new ProtoException($"'{path}': schema type '{type}' is not supported.");
        }
    }

    /// <summary>
    /// Schema 的 <c>format</c>。
    /// </summary>
    private static string? FormatOf(JsonElement node) => node.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String ? format.GetString() : null;

    /// <summary>
    /// 归类一个 Schema 节点：主类型（<c>ref</c> / <c>any</c> / JSON Schema 类型名）、是否可空、有效节点（<c>anyOf</c> 展开后的非 null 分支）。
    /// </summary>
    private static (string Type, bool Nullable, JsonElement Node) Classify(JsonElement schema, string path)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw new ProtoException($"'{path}': the schema must be an object.");
        }

        if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
        {
            // 只支持"某类型或 null"的 anyOf：多于一个非 null 分支没有对应的 protobuf 形态
            JsonElement? branch = null;
            var nullable = false;
            foreach (var option in anyOf.EnumerateArray())
            {
                if (option.ValueKind == JsonValueKind.Object && option.TryGetProperty("type", out var optionType) && optionType.ValueKind == JsonValueKind.String && string.Equals(optionType.GetString(), "null", StringComparison.Ordinal))
                {
                    nullable = true;
                    continue;
                }

                if (branch is not null)
                {
                    throw new ProtoException($"'{path}': anyOf with more than one non-null branch is not supported.");
                }

                branch = option;
            }

            if (branch is null)
            {
                throw new ProtoException($"'{path}': anyOf has no non-null branch.");
            }

            var inner = Classify(branch.Value, path);
            return (inner.Type, inner.Nullable || nullable, inner.Node);
        }

        if (schema.TryGetProperty("$ref", out _))
        {
            return ("ref", false, schema);
        }

        if (!schema.TryGetProperty("type", out var type))
        {
            return ("any", false, schema);
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return (type.GetString()!, false, schema);
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            string? primary = null;
            var nullable = false;
            foreach (var candidate in type.EnumerateArray())
            {
                var name = candidate.GetString();
                if (string.Equals(name, "null", StringComparison.Ordinal))
                {
                    nullable = true;
                }
                else if (primary is null)
                {
                    primary = name;
                }
                else
                {
                    throw new ProtoException($"'{path}': a type union of '{primary}' and '{name}' is not representable in protobuf.");
                }
            }

            return (primary ?? "null", nullable, schema);
        }

        throw new ProtoException($"'{path}': 'type' must be a string or an array of strings.");
    }
}
