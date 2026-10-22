// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;

namespace XiHan.Framework.Kernel.Generators;

/// <summary>
/// 极简 JSON 文本拼装：生成器运行在 netstandard2.0 的编译器进程里，不依赖 System.Text.Json。
/// </summary>
internal sealed class JsonText
{
    /// <summary>
    /// 输出缓冲。
    /// </summary>
    private readonly StringBuilder _builder = new();

    /// <summary>
    /// 每层容器是否已写过成员（决定是否先写逗号）。
    /// </summary>
    private readonly Stack<bool> _hasMembers = new();

    /// <summary>
    /// 开始对象。
    /// </summary>
    public JsonText StartObject(string? name = null)
    {
        Prefix(name);
        _builder.Append('{');
        _hasMembers.Push(false);
        return this;
    }

    /// <summary>
    /// 结束对象。
    /// </summary>
    public JsonText EndObject()
    {
        _hasMembers.Pop();
        _builder.Append('}');
        return this;
    }

    /// <summary>
    /// 开始数组。
    /// </summary>
    public JsonText StartArray(string? name = null)
    {
        Prefix(name);
        _builder.Append('[');
        _hasMembers.Push(false);
        return this;
    }

    /// <summary>
    /// 结束数组。
    /// </summary>
    public JsonText EndArray()
    {
        _hasMembers.Pop();
        _builder.Append(']');
        return this;
    }

    /// <summary>
    /// 写字符串成员或数组元素。
    /// </summary>
    public JsonText String(string? name, string value)
    {
        Prefix(name);
        WriteString(value);
        return this;
    }

    /// <summary>
    /// 写布尔成员。
    /// </summary>
    public JsonText Boolean(string name, bool value)
    {
        Prefix(name);
        _builder.Append(value ? "true" : "false");
        return this;
    }

    /// <summary>
    /// 写数字成员或数组元素。
    /// </summary>
    public JsonText Number(string? name, long value)
    {
        Prefix(name);
        _builder.Append(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    /// <summary>
    /// 写已经是合法 JSON 的片段。
    /// </summary>
    public JsonText Raw(string? name, string json)
    {
        Prefix(name);
        _builder.Append(json);
        return this;
    }

    /// <inheritdoc />
    public override string ToString() => _builder.ToString();

    /// <summary>
    /// 写逗号与成员名。
    /// </summary>
    private void Prefix(string? name)
    {
        if (_hasMembers.Count > 0)
        {
            if (_hasMembers.Peek())
            {
                _builder.Append(',');
            }

            _hasMembers.Pop();
            _hasMembers.Push(true);
        }

        if (name is not null)
        {
            WriteString(name);
            _builder.Append(':');
        }
    }

    /// <summary>
    /// 写带转义的 JSON 字符串。
    /// </summary>
    private void WriteString(string value)
    {
        _builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    _builder.Append("\\\"");
                    break;
                case '\\':
                    _builder.Append("\\\\");
                    break;
                case '\n':
                    _builder.Append("\\n");
                    break;
                case '\r':
                    _builder.Append("\\r");
                    break;
                case '\t':
                    _builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        _builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        _builder.Append(c);
                    }

                    break;
            }
        }

        _builder.Append('"');
    }
}
