// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// JSON 字符串的规范写法
/// </summary>
/// <remarks>
/// 与模板里数组、对象的文本形式同一套转义（RFC-0010「模板」与 RFC-0002 写出，ADR-0236）：<c>"</c> 与 <c>\</c> 加反斜杠，退格、换页、换行、回车、制表符用短写，其余 U+0000–U+001F 与孤立的代理项写成小写十六进制的 <c>\uXXXX</c>，别的字符原样
/// </remarks>
internal static class JsonStringText
{
    /// <summary>
    /// 写一个带引号的 JSON 字符串
    /// </summary>
    public static void Write(StringBuilder builder, string text)
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
                    if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        // 成对的代理项是一个字符，原样写出
                        builder.Append(c).Append(text[i + 1]);
                        i++;
                    }
                    else if (c < ' ' || char.IsSurrogate(c))
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
