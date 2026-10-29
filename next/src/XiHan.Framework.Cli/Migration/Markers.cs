// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 留给人的标记。
/// </summary>
/// <remarks>
/// <c>// xihan-migrate(&lt;规则&gt;): &lt;说明&gt;</c>，插在节点前的独立一行，沿用节点缩进；同一节点同一规则只插一次。
/// </remarks>
internal static class Markers
{
    /// <summary>
    /// 标记前缀。
    /// </summary>
    public const string Prefix = "xihan-migrate";

    /// <summary>
    /// 节点前是否已有该规则、该文案的标记。
    /// </summary>
    public static bool Has(SyntaxNode node, string rule, string message)
        => node.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString() == Line(rule, message));

    /// <summary>
    /// 标记正文。
    /// </summary>
    /// <remarks>
    /// 不带注释前缀；XML 注释里用。
    /// </remarks>
    public static string Text(string rule, string message) => $"{Prefix}({rule}): {message}";

    /// <summary>
    /// 标记行文本（C# 单行注释）。
    /// </summary>
    public static string Line(string rule, string message) => "// " + Text(rule, message);

    /// <summary>
    /// 在节点前插入标记。
    /// </summary>
    /// <remarks>
    /// 已有同规则同文案的标记时原样返回。
    /// </remarks>
    public static TNode With<TNode>(TNode node, string rule, string message, string newLine)
        where TNode : SyntaxNode
    {
        if (Has(node, rule, message))
        {
            return node;
        }

        var leading = node.GetLeadingTrivia().ToList();
        // 缩进取前导 trivia 末尾的空白；标记插在这段缩进之前，缩进继续服务节点本身
        var indent = leading.Count > 0 && leading[^1].IsKind(SyntaxKind.WhitespaceTrivia) ? leading[^1].ToString() : string.Empty;
        var at = indent.Length > 0 ? leading.Count - 1 : leading.Count;
        leading.InsertRange(at,
        [
            SyntaxFactory.Whitespace(indent),
            SyntaxFactory.Comment(Line(rule, message)),
            SyntaxFactory.EndOfLine(newLine),
        ]);
        return node.WithLeadingTrivia(SyntaxFactory.TriviaList(leading));
    }
}
