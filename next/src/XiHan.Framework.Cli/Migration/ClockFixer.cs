// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-04：<c>IClock</c> → <c>TimeProvider</c>；由 <c>IClock</c> 声明的成员上的 <c>.Now</c> / <c>.UtcNow</c> → <c>.GetUtcNow()</c>，<c>.Today</c> → <c>.GetUtcNow().Date</c>。
/// </summary>
internal sealed class ClockFixer : SyntaxFixer
{
    /// <inheritdoc />
    public override string Id => "V4-04";

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        // 先收集本文件里类型为 IClock 的字段、属性、参数与局部变量名：语法级没有类型信息，成员访问按名字判断
        var clocks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>().Where(static f => SimpleName(f.Declaration.Type) == "IClock"))
        {
            foreach (var variable in field.Declaration.Variables)
            {
                clocks.Add(variable.Identifier.ValueText);
            }
        }

        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>().Where(static p => SimpleName(p.Type) == "IClock"))
        {
            clocks.Add(property.Identifier.ValueText);
        }

        foreach (var parameter in root.DescendantNodes().OfType<ParameterSyntax>().Where(static p => p.Type is not null && SimpleName(p.Type) == "IClock"))
        {
            clocks.Add(parameter.Identifier.ValueText);
        }

        foreach (var local in root.DescendantNodes().OfType<VariableDeclarationSyntax>().Where(static v => SimpleName(v.Type) == "IClock"))
        {
            foreach (var variable in local.Variables)
            {
                clocks.Add(variable.Identifier.ValueText);
            }
        }

        if (clocks.Count == 0 && !root.DescendantNodes().OfType<IdentifierNameSyntax>().Any(static n => n.Identifier.ValueText == "IClock"))
        {
            return root;
        }

        return (CompilationUnitSyntax)new Rewriter(this, file, session, clocks).Visit(root);
    }

    /// <summary>
    /// 改类型名与成员访问。
    /// </summary>
    private sealed class Rewriter(ClockFixer fixer, MigrationFile file, MigrationSession session, HashSet<string> clocks) : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.ValueText != "IClock")
            {
                return base.VisitIdentifierName(node);
            }

            session.Changed(fixer.Id, file, LineOf(node));
            return node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, "TimeProvider", node.Identifier.TrailingTrivia));
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var receiver = node.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } self => self.Name.Identifier.ValueText,
                _ => null,
            };
            if (receiver is null || !clocks.Contains(receiver))
            {
                return base.VisitMemberAccessExpression(node);
            }

            // 时钟成员：Now / UtcNow 都是 UTC 时刻，Today 取其日期部分
            switch (node.Name.Identifier.ValueText)
            {
                case "Now" or "UtcNow":
                    session.Changed(fixer.Id, file, LineOf(node));
                    return Invoke(node, "GetUtcNow");
                case "Today":
                    session.Changed(fixer.Id, file, LineOf(node));
                    return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, Invoke(node, "GetUtcNow"), SyntaxFactory.IdentifierName("Date")).WithTriviaFrom(node);
                default:
                    return base.VisitMemberAccessExpression(node);
            }
        }

        /// <summary>
        /// 把 <c>x.Member</c> 改成 <c>x.Method()</c>，保留原 trivia。
        /// </summary>
        private static InvocationExpressionSyntax Invoke(MemberAccessExpressionSyntax access, string method)
            => SyntaxFactory.InvocationExpression(access.WithName(SyntaxFactory.IdentifierName(method)).WithoutTrivia()).WithTriviaFrom(access);
    }
}
