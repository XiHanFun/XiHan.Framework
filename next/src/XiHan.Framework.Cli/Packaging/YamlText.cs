// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 极简 YAML 拼接。
/// </summary>
/// <remarks>
/// 按缩进写行，标量按需加引号，多行文本用块标量。生成的清单只用映射、序列与标量三种结构。
/// </remarks>
internal sealed class YamlText
{
    /// <summary>
    /// 缓冲。
    /// </summary>
    private readonly StringBuilder _builder = new();

    /// <summary>
    /// 写一行，前面补 <paramref name="indent"/> 个空格。
    /// </summary>
    public YamlText Line(int indent, string text)
    {
        _builder.Append(' ', indent).Append(text).Append('\n');
        return this;
    }

    /// <summary>
    /// 写 <c>key: value</c>，值按需加引号。
    /// </summary>
    public YamlText Pair(int indent, string key, string value) => Line(indent, key + ": " + Scalar(value));

    /// <summary>
    /// 写 <c>key: 数字</c>。
    /// </summary>
    public YamlText Pair(int indent, string key, int value) => Line(indent, key + ": " + value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// 写 <c>key: 布尔</c>。
    /// </summary>
    public YamlText Pair(int indent, string key, bool value) => Line(indent, key + ": " + (value ? "true" : "false"));

    /// <summary>
    /// 写块标量 <c>key: |</c> 与缩进后的多行内容。
    /// </summary>
    public YamlText Block(int indent, string key, string content)
    {
        Line(indent, key + ": |");
        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'))
        {
            _builder.Append(' ', indent + 2).Append(line).Append('\n');
        }

        return this;
    }

    /// <summary>
    /// 空行。
    /// </summary>
    public YamlText Blank()
    {
        _builder.Append('\n');
        return this;
    }

    /// <inheritdoc />
    public override string ToString() => _builder.ToString();

    /// <summary>
    /// 标量文本：字母数字与 URL、镜像名常见的安全符号直接写，其余加双引号并转义。
    /// </summary>
    public static string Scalar(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // 看起来像数字（含 YAML 1.1 的六十进制 8080:8080）、布尔、空值，或含 YAML 特殊字符、": " 序列的都加引号，避免被重新解释
        var plain = value.Length > 0
            && (char.IsAsciiLetterOrDigit(value[0]) || value[0] is '/' or '.')
            && value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '/' or ':' or '@' or '=')
            && !(value.Any(char.IsAsciiDigit) && value.All(static c => char.IsAsciiDigit(c) || c is '.' or ':'))
            && !value.Contains(": ", StringComparison.Ordinal)
            && !value.EndsWith(':')
            && value is not ("true" or "false" or "null" or "yes" or "no" or "on" or "off" or "~");
        return plain ? value : "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
