// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XiHan.Framework.Workflow.Abstractions.Values;

/// <summary>
/// 任意 JSON 值的文本形式
/// </summary>
/// <remarks>
/// 规则来自 RFC-0021「表达式」的「值的文本」（ADR-0287）：没有值是空串，标量按 <see cref="WorkflowValues.AsString"/>，数组与对象写成紧凑的 JSON 文本。JSON 文本不交给 <see cref="JsonNode.ToJsonString(JsonSerializerOptions?)"/>：它把非 ASCII 字符与 <c>&lt; &gt; &amp; ' +</c> 写成 <c>\uXXXX</c>、数字照读入时的原文（<c>2.50</c>、<c>1e2</c>）、成员照插入的顺序，别的语言的 JSON 库写不出同样的文本。这里的写法与消息模板里的容器相同（RFC-0010「模板」）
/// </remarks>
internal static class WorkflowValueText
{
    /// <summary>
    /// 数组与对象的最大嵌套层数
    /// </summary>
    /// <remarks>
    /// 与 JSON 读取的上限相同（RFC-0001「JSON 文本」）；更深的容器只可能由进程内的代码构造，写成 <c>null</c>，递归因此有界
    /// </remarks>
    private const int MaxDepth = 64;

    /// <summary>
    /// 值的文本形式
    /// </summary>
    public static string Of(JsonNode? node) => node switch
    {
        null => string.Empty,
        JsonValue value => WorkflowValues.AsString(value),
        _ => Json(node),
    };

    /// <summary>
    /// 数组或对象的紧凑 JSON 文本
    /// </summary>
    private static string Json(JsonNode node)
    {
        var builder = new StringBuilder();
        Write(builder, node, 1);
        return builder.ToString();
    }

    /// <summary>
    /// 递归写一个值的 JSON 文本
    /// </summary>
    /// <remarks>
    /// 没有空白；对象的成员按名字的序数顺序（逐个 UTF-16 码元比较）；数字写规范文本；超过 <see cref="MaxDepth"/> 层的容器写成 <c>null</c>
    /// </remarks>
    /// <param name="builder">输出</param>
    /// <param name="node">要写的值</param>
    /// <param name="depth">本值作为容器时所在的层数，最外层为 1</param>
    private static void Write(StringBuilder builder, JsonNode? node, int depth)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                return;
            case JsonValue value:
                WriteScalar(builder, value);
                return;
            case JsonArray array when depth <= MaxDepth:
                builder.Append('[');
                for (var index = 0; index < array.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    Write(builder, array[index], depth + 1);
                }

                builder.Append(']');
                return;
            case JsonObject obj when depth <= MaxDepth:
                builder.Append('{');
                var first = true;
                foreach (var (name, member) in obj.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteString(builder, name);
                    builder.Append(':');
                    Write(builder, member, depth + 1);
                }

                builder.Append('}');
                return;
            default:
                // 超过层数上限的容器
                builder.Append("null");
                return;
        }
    }

    /// <summary>
    /// 写一个标量
    /// </summary>
    /// <remarks>
    /// 数字写规范文本（超出数字范围的照 JSON 里的原文），布尔写 <c>true</c> / <c>false</c>，字符串按 JSON 字符串写法
    /// </remarks>
    private static void WriteScalar(StringBuilder builder, JsonValue value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                builder.Append(WorkflowValues.AsString(value));
                return;
            case JsonValueKind.String:
                WriteString(builder, WorkflowValues.AsString(value));
                return;
            default:
                // 由带自定义转换器的 CLR 对象构造的值：照它的 JSON 形式
                builder.Append(value.ToJsonString());
                return;
        }
    }

    /// <summary>
    /// 写一个 JSON 字符串，只转义必须转义的字符
    /// </summary>
    /// <remarks>
    /// <c>"</c> 与 <c>\</c> 加反斜杠，退格、换页、换行、回车、制表符用短写，其余 U+0000–U+001F 与孤立的代理项写成小写十六进制的 <c>\uXXXX</c>，别的字符原样（RFC-0010「模板」的 JSON 字符串写法）
    /// </remarks>
    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        // 成对的代理项是一个字符，原样写出
                        builder.Append(c).Append(text[i + 1]);
                        i++;
                    }
                    else if (char.IsSurrogate(c))
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
