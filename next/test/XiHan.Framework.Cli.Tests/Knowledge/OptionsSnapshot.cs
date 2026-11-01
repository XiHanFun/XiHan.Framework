// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace XiHan.Framework.Cli.Tests.Knowledge;

/// <summary>
/// 把 Options 对象写成可比较的文本。
/// </summary>
/// <remarks>
/// 按公开属性递归展开：集合按元素、字典按键值对、标量按不变区域的文本；每次调用新建的集合与字典按内容而不是引用比较。
/// </remarks>
internal static class OptionsSnapshot
{
    /// <summary>
    /// 展开的最大深度。
    /// </summary>
    private const int MaxDepth = 8;

    /// <summary>
    /// 写成文本。
    /// </summary>
    public static string Of(object? value)
    {
        var builder = new StringBuilder();
        Append(builder, value, 0);
        return builder.ToString();
    }

    /// <summary>
    /// 追加一个值。
    /// </summary>
    private static void Append(StringBuilder builder, object? value, int depth)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                return;
            case string text:
                builder.Append('"').Append(text).Append('"');
                return;
            case Delegate:
                builder.Append("delegate");
                return;
            case ReadOnlyMemory<byte> bytes:
                builder.Append(Convert.ToBase64String(bytes.Span));
                return;
            case IFormattable formattable when value.GetType().IsPrimitive || value is decimal or TimeSpan or DateTimeOffset or DateTime or Guid or Enum:
                builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            case bool or char or Uri or Type:
                builder.Append(value);
                return;
            case IEnumerable items:
                builder.Append('[');
                foreach (var item in items)
                {
                    Append(builder, item, depth + 1);
                    builder.Append(',');
                }

                builder.Append(']');
                return;
        }

        if (depth >= MaxDepth)
        {
            builder.Append("...");
            return;
        }

        builder.Append('{');
        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(static p => p.GetIndexParameters().Length == 0).OrderBy(static p => p.Name, StringComparer.Ordinal))
        {
            builder.Append(property.Name).Append('=');
            Append(builder, property.GetValue(value), depth + 1);
            builder.Append(';');
        }

        builder.Append('}');
    }
}
