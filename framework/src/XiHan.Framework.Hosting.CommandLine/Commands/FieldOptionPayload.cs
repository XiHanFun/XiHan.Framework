// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Hosting.CommandLine.Parsing;
using XiHan.Framework.Hosting.CommandLine.Runtime;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Commands;

/// <summary>
/// 把字段选项写成操作的 JSON 载荷
/// </summary>
/// <remarks>
/// 命令行上的值都是文本，请求 Schema 声明了什么类型就写成什么类型，Validate 阶段才认（<c>spec/protocol/commandline.md</c>）。转换不了的值留字符串，由 Validate 阶段报字段级违规；没有 Schema 的操作值都写字符串
/// </remarks>
internal static class FieldOptionPayload
{
    /// <summary>
    /// 展开 <c>$ref</c> 与组合关键字的深度上限
    /// </summary>
    /// <remarks>
    /// 定义可以互相引用；字段的类型用不了这么多层，超过就当作不约束
    /// </remarks>
    private const int MaxBranchDepth = 8;

    /// <summary>
    /// <c>$ref</c> 的本地前缀
    /// </summary>
    private const string DefinitionsPrefix = "#/$defs/";

    /// <summary>
    /// 组合关键字
    /// </summary>
    private static readonly string[] _compositionKeywords = ["allOf", "anyOf", "oneOf"];

    /// <summary>
    /// 严格的 UTF-8 编码
    /// </summary>
    /// <remarks>
    /// 遇到孤立的代理项抛出，不悄悄换成 U+FFFD（ADR-0315）
    /// </remarks>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// 写载荷
    /// </summary>
    /// <remarks>
    /// 保留选项不进载荷；没有任何字段时是空对象。字段的名字或值里有孤立的代理项时不写，与 <c>--json</c> 编码不成同一个用法错误（<see cref="CommandLineErrors.UsageInvalid"/> + <see cref="CommandLineMessages.InvokeJsonInvalid"/>；RFC-0001「写出与转交」，ADR-0315）：写出器缺省把它换成 U+FFFD，操作收到的是另一份载荷
    /// </remarks>
    /// <param name="commandLine">解析后的命令行</param>
    /// <param name="schema">操作的 Schema；没有时值都写字符串</param>
    public static Result<ReadOnlyMemory<byte>> Write(ParsedCommandLine commandLine, OperationSchema? schema)
    {
        // 先核对再写：写出器不会报错，写到一半才发现也只能丢掉
        foreach (var (name, values) in commandLine.Options)
        {
            if (CommandLineInvocation.ReservedOptions.Contains(name))
            {
                continue;
            }

            // 名字有问题时不把它写进原因：带着孤立的代理项的文本到了 stderr 一样被换成 U+FFFD
            if (!IsEncodable(name))
            {
                return LoneSurrogate("an option name has a lone surrogate and cannot be encoded as UTF-8");
            }

            if (!values.All(IsEncodable))
            {
                return LoneSurrogate($"the value of option '--{name}' has a lone surrogate and cannot be encoded as UTF-8");
            }
        }

        // OperationSchema 构造时已校验过是 JSON 对象；没有 Schema 时 root 是 Undefined，下面的查找都得到"不约束"
        using var document = schema is null ? null : JsonDocument.Parse(schema.Request);
        var root = document?.RootElement ?? default;
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (name, values) in commandLine.Options)
            {
                if (CommandLineInvocation.ReservedOptions.Contains(name))
                {
                    continue;
                }

                writer.WritePropertyName(name);
                WriteField(writer, values, root, PropertyOf(root, name));
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    /// <summary>
    /// 字符串能否严格编码成 UTF-8
    /// </summary>
    /// <remarks>
    /// 编码不成的只有孤立的代理项
    /// </remarks>
    private static bool IsEncodable(string text)
    {
        try
        {
            _strictUtf8.GetByteCount(text);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// 字段选项有孤立的代理项时的用法错误
    /// </summary>
    /// <param name="reason">英文的原因</param>
    private static Error LoneSurrogate(string reason)
        => Error.Validation(CommandLineErrors.UsageInvalid, CommandLineMessages.InvokeJsonInvalid, MessageArguments.Of(("reason", reason)));

    /// <summary>
    /// 写一个字段
    /// </summary>
    /// <remarks>
    /// 重复的选项总是数组：目标不是数组时元素写字符串，让 Validate 阶段报出类型不符。单值进只接受数组的字段是一元数组
    /// </remarks>
    private static void WriteField(Utf8JsonWriter writer, IReadOnlyList<string> values, JsonElement root, JsonElement schema)
    {
        var acceptsArray = Accepts(root, schema, "array");
        if (values.Count == 1 && (!acceptsArray || Accepts(root, schema, "string")))
        {
            WriteScalar(writer, values[0], root, schema);
            return;
        }

        var items = acceptsArray ? ItemsOf(root, schema) : default;
        writer.WriteStartArray();
        foreach (var value in values)
        {
            WriteScalar(writer, value, root, items);
        }

        writer.WriteEndArray();
    }

    /// <summary>
    /// 按 Schema 声明的类型写一个文本值
    /// </summary>
    /// <remarks>
    /// 字符串合法时不转换（<c>--name 42</c> 仍是字符串）。整数不经浮点，<c>number</c> 先按 decimal，超出 decimal 才按 double，精度够的值不丢位
    /// </remarks>
    private static void WriteScalar(Utf8JsonWriter writer, string text, JsonElement root, JsonElement schema)
    {
        if (Accepts(root, schema, "string"))
        {
            writer.WriteStringValue(text);
        }
        else if (Accepts(root, schema, "boolean") && bool.TryParse(text, out var flag))
        {
            writer.WriteBooleanValue(flag);
        }
        else if (Accepts(root, schema, "integer") && BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            writer.WriteRawValue(integer.ToString(CultureInfo.InvariantCulture));
        }
        else if (Accepts(root, schema, "number") && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact))
        {
            writer.WriteNumberValue(exact);
        }
        else if (Accepts(root, schema, "number") && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && double.IsFinite(real))
        {
            writer.WriteNumberValue(real);
        }
        else
        {
            // 转换不了不在这里报错：留字符串进管道，Validate 阶段按字段路径报违规，错误码与其他载荷来源一致
            writer.WriteStringValue(text);
        }
    }

    /// <summary>
    /// 根 Schema 里字段对应的 Schema
    /// </summary>
    /// <remarks>
    /// 与校验器同序：先 <c>properties</c>，没有声明的取 <c>additionalProperties</c>。都没有时返回 <see langword="default"/>，表示不约束
    /// </remarks>
    private static JsonElement PropertyOf(JsonElement root, string name)
    {
        JsonElement additional = default;
        foreach (var branch in Branches(root, root, 0))
        {
            if (branch.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(name, out var property))
            {
                return property;
            }

            if (branch.TryGetProperty("additionalProperties", out var rest))
            {
                additional = rest;
            }
        }

        return additional;
    }

    /// <summary>
    /// 数组字段的元素 Schema
    /// </summary>
    /// <remarks>
    /// 没有 <c>items</c> 时返回 <see langword="default"/>，表示元素不约束
    /// </remarks>
    private static JsonElement ItemsOf(JsonElement root, JsonElement schema)
    {
        foreach (var branch in Branches(root, schema, 0))
        {
            if (branch.TryGetProperty("items", out var items))
            {
                return items;
            }
        }

        return default;
    }

    /// <summary>
    /// Schema 的任一分支是否声明了这个类型
    /// </summary>
    private static bool Accepts(JsonElement root, JsonElement schema, string typeName)
    {
        foreach (var branch in Branches(root, schema, 0))
        {
            if (branch.TryGetProperty("type", out var type) && Declares(type, typeName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>type</c> 关键字（单个名字或名字数组）是否含这个类型
    /// </summary>
    private static bool Declares(JsonElement type, string typeName)
    {
        if (type.ValueKind == JsonValueKind.String)
        {
            return type.ValueEquals(typeName);
        }

        if (type.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var candidate in type.EnumerateArray())
        {
            if (candidate.ValueKind == JsonValueKind.String && candidate.ValueEquals(typeName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 一个 Schema 节点及其 <c>$ref</c> 与组合关键字展开出的全部分支
    /// </summary>
    /// <remarks>
    /// 这里只判断文本该写成什么类型，分支一律取并集，<c>allOf</c> 的交集语义留给 Validate 阶段
    /// </remarks>
    private static IEnumerable<JsonElement> Branches(JsonElement root, JsonElement schema, int depth)
    {
        if (schema.ValueKind != JsonValueKind.Object || depth > MaxBranchDepth)
        {
            yield break;
        }

        yield return schema;
        if (schema.TryGetProperty("$ref", out var reference) && TryResolve(root, reference, out var target))
        {
            foreach (var branch in Branches(root, target, depth + 1))
            {
                yield return branch;
            }
        }

        foreach (var keyword in _compositionKeywords)
        {
            if (!schema.TryGetProperty(keyword, out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var candidate in candidates.EnumerateArray())
            {
                foreach (var branch in Branches(root, candidate, depth + 1))
                {
                    yield return branch;
                }
            }
        }
    }

    /// <summary>
    /// 解析本地 <c>$ref</c>（只支持 <c>#/$defs/名字</c>，与校验器一致）
    /// </summary>
    private static bool TryResolve(JsonElement root, JsonElement reference, out JsonElement target)
    {
        target = default;
        var text = reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
        return text is not null
            && text.StartsWith(DefinitionsPrefix, StringComparison.Ordinal)
            && root.TryGetProperty("$defs", out var definitions)
            && definitions.ValueKind == JsonValueKind.Object
            && definitions.TryGetProperty(text[DefinitionsPrefix.Length..], out target);
    }
}
