// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 作用于 C# 语法树的规则：解析、改写、回写；语法树没变就不碰文件。
/// </summary>
internal abstract class SyntaxFixer : MigrationFixer
{
    /// <inheritdoc />
    public override bool Handles(MigrationFile file) => file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string Apply(MigrationFile file, string text, MigrationSession session)
    {
        var root = CSharpSyntaxTree.ParseText(text, path: file.Path).GetCompilationUnitRoot();
        var rewritten = Rewrite(root, file, session);
        return ReferenceEquals(rewritten, root) ? text : rewritten.ToFullString();
    }

    /// <summary>
    /// 改写语法树；没有改动时返回传入的根。
    /// </summary>
    protected abstract CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session);

    /// <summary>
    /// 节点起始位置的 1 起行号。
    /// </summary>
    protected static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    /// <summary>
    /// 补上缺少的顶层 <c>using</c>：按字典序插入，文件头注释留在第一条上；原来没有 using 时，后面的声明前空一行。
    /// </summary>
    protected static CompilationUnitSyntax WithUsings(CompilationUnitSyntax root, IEnumerable<string> namespaces, string newLine)
    {
        ArgumentNullException.ThrowIfNull(root);
        var existing = root.Usings.Select(static u => u.Name?.ToString()).ToHashSet(StringComparer.Ordinal);
        var missing = namespaces.Where(ns => !existing.Contains(ns)).Distinct(StringComparer.Ordinal).OrderBy(static ns => ns, StringComparer.Ordinal).ToList();
        if (missing.Count == 0)
        {
            return root;
        }

        var first = root.GetFirstToken();
        var header = first.LeadingTrivia;
        var usings = root.Usings.ToList();
        if (usings.Count > 0)
        {
            // 头注释先从第一条 using 上摘下来，排好序后挂回新的第一条
            usings[0] = usings[0].WithLeadingTrivia(SyntaxFactory.TriviaList());
        }
        else
        {
            root = root.ReplaceToken(first, first.WithLeadingTrivia(SyntaxFactory.EndOfLine(newLine)));
        }

        foreach (var ns in missing)
        {
            var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns).WithLeadingTrivia(SyntaxFactory.Space)).WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine));
            var index = usings.FindIndex(u => u.Alias is null && u.StaticKeyword.IsKind(SyntaxKind.None) && string.CompareOrdinal(u.Name?.ToString(), ns) > 0);
            usings.Insert(index < 0 ? usings.Count : index, directive);
        }

        usings[0] = usings[0].WithLeadingTrivia(header);
        return root.WithUsings(SyntaxFactory.List(usings));
    }
}
