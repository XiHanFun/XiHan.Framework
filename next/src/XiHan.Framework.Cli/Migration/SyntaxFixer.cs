// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 作用于 C# 语法树的规则：解析、改写、回写；语法树没变就不碰文件。
/// </summary>
internal abstract class SyntaxFixer : V4Fixer
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
    /// 类型名的末段（去掉命名空间与泛型参数），如 <c>XiHan.Framework.Core.Modularity.XiHanModule</c> → <c>XiHanModule</c>。
    /// </summary>
    protected static string SimpleName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        NullableTypeSyntax nullable => SimpleName(nullable.ElementType),
        _ => type.ToString(),
    };

    /// <summary>
    /// 特性名（去掉命名空间与 <c>Attribute</c> 后缀）。
    /// </summary>
    protected static string AttributeName(AttributeSyntax attribute)
    {
        var name = SimpleName(attribute.Name);
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;
    }

    /// <summary>
    /// 从成员上删除名字满足 <paramref name="remove"/> 的特性；整个特性列表被删掉时，把它的前导注释（文档注释）转交给后面的元素，缩进不重复。
    /// </summary>
    protected static TMember RemoveAttributes<TMember>(TMember member, Func<string, bool> remove, out int removed)
        where TMember : MemberDeclarationSyntax
    {
        removed = 0;
        var kept = new List<AttributeListSyntax>();
        var carried = SyntaxFactory.TriviaList();
        foreach (var list in member.AttributeLists)
        {
            var attributes = list.Attributes.Where(attribute => !remove(AttributeName(attribute))).ToList();
            removed += list.Attributes.Count - attributes.Count;
            if (attributes.Count == 0)
            {
                carried = carried.AddRange(WithoutTrailingIndent(list.GetLeadingTrivia()));
                continue;
            }

            var next = attributes.Count == list.Attributes.Count ? list : list.WithAttributes(SyntaxFactory.SeparatedList(attributes));
            kept.Add(carried.Count > 0 ? next.WithLeadingTrivia(carried.AddRange(next.GetLeadingTrivia())) : next);
            carried = SyntaxFactory.TriviaList();
        }

        if (removed == 0)
        {
            return member;
        }

        var result = (TMember)member.WithAttributeLists(SyntaxFactory.List(kept));
        if (carried.Count > 0)
        {
            var first = result.GetFirstToken();
            result = result.ReplaceToken(first, first.WithLeadingTrivia(carried.AddRange(first.LeadingTrivia)));
        }

        return result;
    }

    /// <summary>
    /// 节点所在行的缩进（前导 trivia 末尾的空白）。
    /// </summary>
    protected static string IndentOf(SyntaxNode node)
    {
        var leading = node.GetLeadingTrivia();
        return leading.Count > 0 && leading[^1].IsKind(SyntaxKind.WhitespaceTrivia) ? leading[^1].ToString() : string.Empty;
    }

    /// <summary>
    /// 节点前是否已空一行（前导 trivia 以换行开头）。
    /// </summary>
    protected static bool StartsWithBlankLine(SyntaxNode node)
    {
        var leading = node.GetLeadingTrivia();
        return leading.Count > 0 && leading[0].IsKind(SyntaxKind.EndOfLineTrivia);
    }

    /// <summary>
    /// 把成员源码解析成成员声明列表；源码里的缩进与换行原样进入 trivia。
    /// </summary>
    protected static SyntaxList<MemberDeclarationSyntax> ParseMembers(string members, string newLine)
    {
        var unit = CSharpSyntaxTree.ParseText("class __ {" + newLine + members + "}").GetCompilationUnitRoot();
        return ((ClassDeclarationSyntax)unit.Members[0]).Members;
    }

    /// <summary>
    /// 补上缺少的顶层 <c>using</c>：按字典序插入，文件头注释留在第一条上；原来没有 using 时，后面的声明前空一行。
    /// </summary>
    protected static CompilationUnitSyntax WithUsings(CompilationUnitSyntax root, IEnumerable<string> namespaces, string newLine)
    {
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

    /// <summary>
    /// 从基类列表删除名字满足 <paramref name="remove"/> 的类型（连同其后的逗号，保留其余分隔符的空格）；列表空了就连冒号一起删掉，把列表的尾随 trivia 还给前一个 token。
    /// </summary>
    protected static TDeclaration RemoveBaseTypes<TDeclaration>(TDeclaration node, Func<string, bool> remove)
        where TDeclaration : TypeDeclarationSyntax
    {
        if (node.BaseList is null)
        {
            return node;
        }

        var items = node.BaseList.Types.GetWithSeparators();
        var kept = new List<SyntaxNodeOrToken>();
        var removed = 0;
        for (var index = 0; index < items.Count; index += 2)
        {
            var type = (BaseTypeSyntax)items[index].AsNode()!;
            var separator = index + 1 < items.Count ? items[index + 1] : default;
            if (remove(SimpleName(type.Type)))
            {
                removed++;
                continue;
            }

            kept.Add(type);
            if (separator.IsToken)
            {
                kept.Add(separator);
            }
        }

        if (removed == 0)
        {
            return node;
        }

        // 最后一个被删掉时，前一个逗号也多余；原列表的尾随 trivia 还给新的最后一个类型
        if (kept.Count > 0 && kept[^1].IsToken)
        {
            kept.RemoveAt(kept.Count - 1);
            var last = (BaseTypeSyntax)kept[^1].AsNode()!;
            kept[^1] = last.WithTrailingTrivia(node.BaseList.GetTrailingTrivia());
        }

        if (kept.Count > 0)
        {
            return (TDeclaration)node.WithBaseList(node.BaseList.WithTypes(SyntaxFactory.SeparatedList<BaseTypeSyntax>(kept)));
        }

        var annotation = new SyntaxAnnotation();
        var before = node.BaseList.GetFirstToken().GetPreviousToken();
        var annotated = node.ReplaceToken(before, before.WithAdditionalAnnotations(annotation));
        var trailing = annotated.BaseList!.GetTrailingTrivia();
        var stripped = (TDeclaration)annotated.WithBaseList(null);
        var token = stripped.GetAnnotatedTokens(annotation).Single();
        return stripped.ReplaceToken(token, token.WithTrailingTrivia(trailing));
    }

    /// <summary>
    /// 去掉前导 trivia 末尾的缩进空白：转交给后一个元素时，由后者自己的缩进负责。
    /// </summary>
    private static SyntaxTriviaList WithoutTrailingIndent(SyntaxTriviaList trivia)
    {
        var list = trivia.ToList();
        while (list.Count > 0 && list[^1].IsKind(SyntaxKind.WhitespaceTrivia))
        {
            list.RemoveAt(list.Count - 1);
        }

        return SyntaxFactory.TriviaList(list);
    }
}
