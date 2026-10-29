// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Kernel.Generators.Json;

/// <summary>
/// 从符号的 XML 文档注释取 Schema 的 <c>description</c>。
/// </summary>
/// <remarks>
/// 取 <c>&lt;summary&gt;</c> 或某个 <c>&lt;param&gt;</c> 的纯文本：<c>&lt;see cref&gt;</c> 取最后一段名字，<c>&lt;see langword&gt;</c>、<c>&lt;c&gt;</c>、<c>&lt;paramref&gt;</c>、<c>&lt;typeparamref&gt;</c> 取文本，空白折叠成一个空格。
/// 项目没有生成文档注释、符号没有注释或注释不是合法 XML 时为 <see langword="null"/>。
/// </remarks>
internal static class DocumentationText
{
    /// <summary>
    /// 符号的 <c>&lt;summary&gt;</c> 文本。
    /// </summary>
    public static string? Summary(ISymbol symbol) => Text(Load(symbol)?.Descendants("summary").FirstOrDefault());

    /// <summary>
    /// 类型上某个 <c>&lt;param&gt;</c> 的文本（位置记录的主构造参数）。
    /// </summary>
    public static string? Parameter(ISymbol type, string name)
        => Text(Load(type)?.Descendants("param").FirstOrDefault(p => string.Equals((string?)p.Attribute("name"), name, StringComparison.Ordinal)));

    /// <summary>
    /// 解析符号的文档注释；没有或不合法时为 <see langword="null"/>。
    /// </summary>
    private static XElement? Load(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: false);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XElement.Parse(xml, LoadOptions.None);
        }
        catch (XmlException)
        {
            // 注释写错时编译器另有警告，这里不让它影响生成
            return null;
        }
    }

    /// <summary>
    /// 元素的纯文本；为空时为 <see langword="null"/>。
    /// </summary>
    private static string? Text(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        Append(builder, element);
        var collapsed = new StringBuilder(builder.Length);
        var space = false;
        var newline = false;
        foreach (var ch in builder.ToString())
        {
            if (char.IsWhiteSpace(ch))
            {
                space = collapsed.Length > 0;
                newline |= ch is '\n' or '\r';
                continue;
            }

            // 中文在注释里换行不是词间空格：换行两侧有一侧是中日韩字符或全角标点时直接相连
            if (space && !(newline && (IsWide(collapsed[collapsed.Length - 1]) || IsWide(ch))))
            {
                collapsed.Append(' ');
            }

            space = false;
            newline = false;
            collapsed.Append(ch);
        }

        return collapsed.Length > 0 ? collapsed.ToString() : null;
    }

    /// <summary>
    /// 中日韩字符与全角标点。
    /// </summary>
    private static bool IsWide(char ch) => ch is (>= '\u2E80' and <= '\u9FFF') or (>= '\uAC00' and <= '\uD7AF') or (>= '\uF900' and <= '\uFAFF') or (>= '\uFF00' and <= '\uFFEF');

    /// <summary>
    /// 把节点的文本追加进去，按标签取引用的名字或文字。
    /// </summary>
    private static void Append(StringBuilder builder, XElement element)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement child when child.Name.LocalName is "see" or "seealso":
                    if ((string?)child.Attribute("langword") is { } langword)
                    {
                        builder.Append(langword);
                    }
                    else if (!child.IsEmpty)
                    {
                        Append(builder, child);
                    }
                    else if ((string?)child.Attribute("cref") is { } cref)
                    {
                        builder.Append(NameOf(cref));
                    }
                    else if ((string?)child.Attribute("href") is { } href)
                    {
                        builder.Append(href);
                    }

                    break;
                case XElement child when child.Name.LocalName is "paramref" or "typeparamref":
                    builder.Append((string?)child.Attribute("name"));
                    break;
                case XElement child when child.Name.LocalName is "para" or "br":
                    builder.Append(' ');
                    Append(builder, child);
                    builder.Append(' ');
                    break;
                case XElement child:
                    Append(builder, child);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// <c>cref</c> 的显示名：去掉 <c>T:</c> 等前缀与参数表，取最后一段，泛型元数去掉。
    /// </summary>
    private static string NameOf(string cref)
    {
        var name = cref.Length > 2 && cref[1] == ':' ? cref.Substring(2) : cref;
        var parameters = name.IndexOf('(');
        if (parameters >= 0)
        {
            name = name.Substring(0, parameters);
        }

        var dot = name.LastIndexOf('.');
        if (dot >= 0)
        {
            name = name.Substring(dot + 1);
        }

        var arity = name.IndexOf('`');
        return arity >= 0 ? name.Substring(0, arity) : name;
    }
}
