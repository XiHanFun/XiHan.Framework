// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-02：实体成员改名（<c>BasicId</c> → <c>Id</c> 等）、<c>RowVersion</c> / <c>TenantId</c> 改字符串、4.x Sugar 基类展开成协议接口与属性、标记接口换名。
/// </summary>
internal sealed class EntityProtocolFixer : SyntaxFixer
{
    /// <inheritdoc />
    public override string Id => "V4-02";

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        var rewriter = new Rewriter(this, file, session);
        var result = (CompilationUnitSyntax)rewriter.Visit(root);
        return rewriter.Usings.Count > 0 ? WithUsings(result, rewriter.Usings, file.NewLine) : result;
    }

    /// <summary>
    /// 主键类型：泛型基类的第一个类型参数；非泛型默认 <c>long</c>。
    /// </summary>
    private static string KeyTypeOf(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.TypeArgumentList.Arguments[0].ToString(),
        QualifiedNameSyntax qualified => KeyTypeOf(qualified.Right),
        _ => "long",
    };

    /// <summary>
    /// 是否 <c>long</c> / <c>long?</c>。
    /// </summary>
    private static bool IsLong(TypeSyntax type, out bool nullable)
    {
        nullable = type is NullableTypeSyntax;
        var inner = type is NullableTypeSyntax n ? n.ElementType : type;
        return inner.ToString() is "long" or "Int64" or "System.Int64";
    }

    /// <summary>
    /// 逐节点改写。
    /// </summary>
    private sealed class Rewriter(EntityProtocolFixer fixer, MigrationFile file, MigrationSession session) : CSharpSyntaxRewriter
    {
        /// <summary>
        /// 展开后要补的 using。
        /// </summary>
        public HashSet<string> Usings { get; } = new(StringComparer.Ordinal);

        /// <inheritdoc />
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (!V4EntityBases.Renames.TryGetValue(node.Identifier.ValueText, out var renamed))
            {
                return base.VisitIdentifierName(node);
            }

            session.Changed(fixer.Id, file, LineOf(node));
            return node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, renamed, node.Identifier.TrailingTrivia));
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitParameter(ParameterSyntax node)
        {
            var visited = (ParameterSyntax)base.VisitParameter(node)!;
            return TryRename(visited.Identifier, node, out var identifier) ? visited.WithIdentifier(identifier) : visited;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            var visited = (VariableDeclaratorSyntax)base.VisitVariableDeclarator(node)!;
            return TryRename(visited.Identifier, node, out var identifier) ? visited.WithIdentifier(identifier) : visited;
        }

        /// <summary>
        /// 声明标识符（记录位置参数、参数、字段与局部变量）按改名表改名；只有属性与标识符用法不够，记录参数等声明也要跟着改。
        /// </summary>
        private bool TryRename(SyntaxToken identifier, SyntaxNode original, out SyntaxToken renamed)
        {
            renamed = identifier;
            if (!V4EntityBases.Renames.TryGetValue(identifier.ValueText, out var name))
            {
                return false;
            }

            session.Changed(fixer.Id, file, LineOf(original));
            renamed = SyntaxFactory.Identifier(identifier.LeadingTrivia, name, identifier.TrailingTrivia);
            return true;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
            var name = visited.Identifier.ValueText;
            if (V4EntityBases.Renames.TryGetValue(name, out var renamed))
            {
                session.Changed(fixer.Id, file, LineOf(node));
                visited = visited.WithIdentifier(SyntaxFactory.Identifier(visited.Identifier.LeadingTrivia, renamed, visited.Identifier.TrailingTrivia));
                name = renamed;
            }

            // RowVersion(long) → ConcurrencyStamp(string)，TenantId(long) → string；非空的补 string.Empty 初始化
            if (name is not ("ConcurrencyStamp" or "TenantId") || !IsLong(visited.Type, out var nullable))
            {
                return visited;
            }

            session.Changed(fixer.Id, file, LineOf(node));
            visited = visited.WithType(SyntaxFactory.ParseTypeName(nullable ? "string?" : "string").WithTriviaFrom(visited.Type));
            // 接口成员不能有初始化器
            if (!nullable && visited.Initializer is null && visited.ExpressionBody is null && visited.AccessorList is not null && node.Parent is not InterfaceDeclarationSyntax)
            {
                var trailing = visited.AccessorList.GetTrailingTrivia();
                visited = visited
                    .WithAccessorList(visited.AccessorList.WithTrailingTrivia(SyntaxFactory.Space))
                    .WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.Token(SyntaxKind.EqualsToken).WithTrailingTrivia(SyntaxFactory.Space), SyntaxFactory.ParseExpression("string.Empty")))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(trailing));
            }

            var message = name == "TenantId" ? CliMessages.MigrateMarkerTenantIdType.L() : CliMessages.MigrateMarkerConcurrencyStampColumn.L();
            session.Marked(fixer.Id, file, LineOf(node), message);
            return Markers.With(visited, fixer.Id, message, file.NewLine);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            var visited = (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!;
            if (visited.BaseList is null)
            {
                return visited;
            }

            var types = new List<BaseTypeSyntax>();
            var messages = new List<string>();
            if (!ReplaceInterfaces(visited.BaseList, node, types, messages))
            {
                return visited;
            }

            visited = visited.WithBaseList(RebuildBaseList(visited.BaseList, types.DistinctBy(static type => type.Type.ToString(), StringComparer.Ordinal).ToList()));
            return Mark(visited, node, messages);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            if (visited.BaseList is null)
            {
                return visited;
            }

            var newLine = file.NewLine;
            var indent = IndentOf(node) + "    ";
            var types = new List<BaseTypeSyntax>();
            var generated = new StringBuilder();
            var messages = new List<string>();
            var expanded = false;
            var tenant = false;
            var touched = false;
            foreach (var baseType in visited.BaseList.Types)
            {
                var name = SimpleName(baseType.Type);
                if (V4EntityBases.TryExpand(name, KeyTypeOf(baseType.Type), indent, newLine, out var interfaces, out var members, out var multiTenant))
                {
                    touched = true;
                    expanded = true;
                    tenant |= multiTenant;
                    session.Changed(fixer.Id, file, LineOf(node));
                    types.AddRange(interfaces.Select(static i => SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(i))));
                    if (generated.Length > 0)
                    {
                        generated.Append(newLine);
                    }

                    generated.Append(members);
                    continue;
                }

                if (V4EntityBases.Interfaces.TryGetValue(name, out var replacement))
                {
                    touched = true;
                    if (replacement is null)
                    {
                        // 保留为应用接口：只留标记，不算改动，再跑一遍才是零改动
                        messages.Add(CliMessages.MigrateMarkerAppInterface.L(("name", name)));
                        types.Add(baseType);
                    }
                    else
                    {
                        session.Changed(fixer.Id, file, LineOf(node));
                        types.Add(baseType.WithType(SyntaxFactory.ParseTypeName(replacement).WithTriviaFrom(baseType.Type)));
                        // 换成的 5.0 接口在 Domain.Entities 里
                        Usings.Add("XiHan.Framework.Domain.Entities");
                    }

                    continue;
                }

                types.Add(baseType);
            }

            if (!touched)
            {
                return visited;
            }

            visited = visited.WithBaseList(RebuildBaseList(visited.BaseList, types.DistinctBy(static type => type.Type.ToString(), StringComparer.Ordinal).ToList()));
            if (expanded)
            {
                Usings.Add("SqlSugar");
                Usings.Add("XiHan.Framework.Domain.Entities");
                var members = ParseMembers(generated.ToString(), newLine).ToList();
                var existing = visited.Members.Select(member => member is ConstructorDeclarationSyntax constructor ? Rebase(constructor, newLine) : member).ToList();
                if (existing.Count > 0 && !StartsWithBlankLine(existing[0]))
                {
                    members[^1] = members[^1].WithTrailingTrivia(members[^1].GetTrailingTrivia().Add(SyntaxFactory.EndOfLine(newLine)));
                }

                visited = visited.WithMembers(SyntaxFactory.List(members.Concat(existing)));
                messages.Add(CliMessages.MigrateMarkerConcurrencyStampColumn.L());
                if (tenant)
                {
                    messages.Add(CliMessages.MigrateMarkerTenantIdType.L());
                }
            }

            return Mark(visited, node, messages);
        }

        /// <summary>
        /// 基类列表里的 4.x 标记接口换成 5.0 接口；没有对应物的保留并记文案。返回是否有改动。
        /// </summary>
        private bool ReplaceInterfaces(BaseListSyntax baseList, SyntaxNode original, List<BaseTypeSyntax> types, List<string> messages)
        {
            var touched = false;
            foreach (var baseType in baseList.Types)
            {
                var name = SimpleName(baseType.Type);
                if (V4EntityBases.Interfaces.TryGetValue(name, out var replacement))
                {
                    touched = true;
                    if (replacement is null)
                    {
                        messages.Add(CliMessages.MigrateMarkerAppInterface.L(("name", name)));
                        types.Add(baseType);
                    }
                    else
                    {
                        session.Changed(fixer.Id, file, LineOf(original));
                        types.Add(baseType.WithType(SyntaxFactory.ParseTypeName(replacement).WithTriviaFrom(baseType.Type)));
                        Usings.Add("XiHan.Framework.Domain.Entities");
                    }

                    continue;
                }

                types.Add(baseType);
            }

            return touched;
        }

        /// <summary>
        /// 构造函数的 <c>: base(id)</c> / <c>: base(id, createdId)</c> 改成体内赋值 <c>Id = id;</c> / <c>CreatedId = createdId;</c>。
        /// </summary>
        private static ConstructorDeclarationSyntax Rebase(ConstructorDeclarationSyntax constructor, string newLine)
        {
            if (constructor.Initializer is not { } initializer || !initializer.IsKind(SyntaxKind.BaseConstructorInitializer))
            {
                return constructor;
            }

            var arguments = initializer.ArgumentList.Arguments;
            var result = constructor.WithInitializer(null);
            // 初始化器在参数表同一行时，参数表接过它的尾随换行
            if (!constructor.ParameterList.GetTrailingTrivia().Any(SyntaxKind.EndOfLineTrivia))
            {
                result = result.WithParameterList(constructor.ParameterList.WithTrailingTrivia(initializer.GetTrailingTrivia()));
            }

            if (result.Body is null || arguments.Count == 0)
            {
                return result;
            }

            var indent = IndentOf(constructor) + "    ";
            var assignments = new List<StatementSyntax>();
            var targets = new[] { "Id", "CreatedId" };
            for (var index = 0; index < Math.Min(arguments.Count, targets.Length); index++)
            {
                assignments.Add(SyntaxFactory.ParseStatement($"{targets[index]} = {arguments[index].Expression};")
                    .WithLeadingTrivia(SyntaxFactory.Whitespace(indent))
                    .WithTrailingTrivia(SyntaxFactory.EndOfLine(newLine)));
            }

            return result.WithBody(result.Body.WithStatements(SyntaxFactory.List(assignments.Concat(result.Body.Statements))));
        }

        /// <summary>
        /// 在类型声明前留标记并记报告。
        /// </summary>
        private TNode Mark<TNode>(TNode node, SyntaxNode original, IReadOnlyList<string> messages)
            where TNode : SyntaxNode
        {
            foreach (var message in messages)
            {
                if (Markers.Has(node, fixer.Id, message))
                {
                    continue;
                }

                session.Marked(fixer.Id, file, LineOf(original), message);
                node = Markers.With(node, fixer.Id, message, file.NewLine);
            }

            return node;
        }
    }
}
