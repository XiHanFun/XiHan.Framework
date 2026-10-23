// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-05：删除 <c>[UnitOfWork]</c>（每个操作默认一个工作单元）；<c>IUnitOfWorkManager</c> 的声明与 <c>Begin</c> 调用留标记。
/// </summary>
internal sealed class UnitOfWorkFixer : SyntaxFixer
{
    /// <inheritdoc />
    public override string Id => "V4-05";

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        var rewriter = new Rewriter(this, file, session);
        return (CompilationUnitSyntax)rewriter.Visit(root);
    }

    /// <summary>
    /// 逐成员删除特性并对工作单元管理器留标记。
    /// </summary>
    private sealed class Rewriter(UnitOfWorkFixer fixer, MigrationFile file, MigrationSession session) : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) => Strip((MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!, LineOf(node));

        /// <inheritdoc />
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) => Strip((ClassDeclarationSyntax)base.VisitClassDeclaration(node)!, LineOf(node));

        /// <inheritdoc />
        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => Strip((InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!, LineOf(node));

        /// <inheritdoc />
        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
            => SimpleName(node.Declaration.Type) == "IUnitOfWorkManager" ? Mark(node, LineOf(node)) : base.VisitFieldDeclaration(node);

        /// <inheritdoc />
        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
            => BeginsUnitOfWork(node) ? Mark(node, LineOf(node)) : base.VisitLocalDeclarationStatement(node);

        /// <inheritdoc />
        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
            => BeginsUnitOfWork(node) ? Mark(node, LineOf(node)) : base.VisitExpressionStatement(node);

        /// <summary>
        /// 语句里是否有对工作单元管理器的 <c>Begin</c> 调用（按接收者名字判断，语法级没有类型信息）。
        /// </summary>
        private static bool BeginsUnitOfWork(StatementSyntax statement)
            => statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(static invocation =>
                invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Begin" } access
                && access.Expression.ToString().Contains("UnitOfWorkManager", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 删除 <c>[UnitOfWork]</c> 并记改动。
        /// </summary>
        private TMember Strip<TMember>(TMember member, int line)
            where TMember : MemberDeclarationSyntax
        {
            var result = RemoveAttributes(member, static name => name == "UnitOfWork", out var removed);
            for (var count = 0; count < removed; count++)
            {
                session.Changed(fixer.Id, file, line);
            }

            return result;
        }

        /// <summary>
        /// 留工作单元管理器的标记。
        /// </summary>
        private TNode Mark<TNode>(TNode node, int line)
            where TNode : SyntaxNode
        {
            var message = CliMessages.MigrateMarkerUnitOfWorkManager.L();
            if (Markers.Has(node, fixer.Id, message))
            {
                return node;
            }

            session.Marked(fixer.Id, file, line, message);
            return Markers.With(node, fixer.Id, message, file.NewLine);
        }
    }
}
