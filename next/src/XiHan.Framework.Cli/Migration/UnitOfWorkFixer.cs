// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-05：删除 <c>[UnitOfWork]</c>（每个操作默认一个工作单元）；<c>IUnitOfWorkManager</c> 的声明与 <c>Begin</c> 调用留标记。
/// 只读 <c>Current</c> 的单例消费方（显式 <c>AddSingleton</c> / <c>TryAddSingleton</c> / <c>AddHostedService</c>，或沿基类链继承到单例存期）改为注入生成的 <c>ICurrentUnitOfWorkAccessor</c>：
/// 在它们引用链最底层的项目生成 <c>Data/ICurrentUnitOfWorkAccessor.cs</c> 与 <c>CurrentUnitOfWorkAccessor.cs</c>（从当前调用的服务作用域读应用的 <c>IUnitOfWorkManager.Current</c>），按单例进 V4-03 的注册文件。
/// </summary>
internal sealed class UnitOfWorkFixer : SyntaxFixer
{
    /// <summary>
    /// 4.x 的工作单元管理器接口名。
    /// </summary>
    private const string Manager = "IUnitOfWorkManager";

    /// <summary>
    /// 生成的访问器接口名。
    /// </summary>
    private const string Accessor = "ICurrentUnitOfWorkAccessor";

    /// <summary>
    /// 把实现类型注册为单例的方法名（实现类型是最后一个类型实参）。
    /// </summary>
    private static readonly HashSet<string> _singletonRegistrations = new(StringComparer.Ordinal) { "AddSingleton", "TryAddSingleton", "AddHostedService" };

    /// <inheritdoc />
    public override string Id => "V4-05";

    /// <inheritdoc />
    public override bool Handles(MigrationFile file) => base.Handles(file) || file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override void Scan(MigrationFile file, string text, MigrationSession session)
    {
        if (file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            session.ScanProjectReferences(file, text);
            return;
        }

        var root = CSharpSyntaxTree.ParseText(text, path: file.Path).GetCompilationUnitRoot();
        // 显式单例注册：AddSingleton<T>() / TryAddSingleton<TService, T>() / AddHostedService<T>() 的最后一个类型实参
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var generic = invocation.Expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => name,
                GenericNameSyntax name => name,
                _ => null,
            };
            if (generic is not null && _singletonRegistrations.Contains(generic.Identifier.ValueText) && generic.TypeArgumentList.Arguments.Count > 0)
            {
                session.ExplicitSingletons.Add(SimpleName(generic.TypeArgumentList.Arguments[^1]));
            }
        }

        // 应用已承接的 IUnitOfWorkManager：生成的访问器补它的 using
        foreach (var declaration in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>().Where(static declaration => declaration.Identifier.ValueText == Manager))
        {
            session.UnitOfWorkManagerNamespace ??= declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        }

        if (file.ProjectDirectory is null)
        {
            return;
        }

        // 消费类：字段、属性、构造函数或主构造函数参数的类型是 IUnitOfWorkManager
        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(Consumes))
        {
            var name = declaration.Identifier.ValueText;
            var begins = declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(BeginsUnitOfWork);
            session.UnitOfWorkManagerConsumers[name] = (file.ProjectDirectory, begins || (session.UnitOfWorkManagerConsumers.TryGetValue(name, out var existing) && existing.Begins));
        }
    }

    /// <inheritdoc />
    public override void Prepare(MigrationSession session)
    {
        // 只读 Current 的单例消费方：显式单例注册，或沿基类链继承到单例存期（V4-03 的扫描）
        var singletons = session.UnitOfWorkManagerConsumers
            .Where(pair => !pair.Value.Begins && (session.ExplicitSingletons.Contains(pair.Key) || session.Services.InheritedLifetime(pair.Key) == "Singleton"))
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToList();
        if (singletons.Count == 0)
        {
            return;
        }

        var users = singletons.Select(static pair => pair.Value.ProjectDirectory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var providers = users.ToDictionary(static user => user, user => session.LowestProvider(user, users), StringComparer.OrdinalIgnoreCase);
        foreach (var (name, (project, _)) in singletons)
        {
            session.UnitOfWorkAccessorConsumers[name] = V4ServicesGenerator.Names(providers[project]).Project + ".Data";
        }

        foreach (var provider in providers.Values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static provider => provider, StringComparer.Ordinal))
        {
            var directory = Path.Combine(provider, "Data");
            // 已生成过（再跑一遍）就不动
            if (File.Exists(Path.Combine(directory, V4UnitOfWorkAccessorGenerator.FileNames[0])))
            {
                continue;
            }

            var (project, _) = V4ServicesGenerator.Names(provider);
            var ns = project + ".Data";
            var newLine = session.ProjectNewLine(provider);
            var message = CliMessages.MigrateMarkerUnitOfWorkAccessor.L();
            foreach (var (fileName, content) in V4UnitOfWorkAccessorGenerator.Generate(ns, session.UnitOfWorkManagerNamespace, Markers.Line(Id, message), newLine))
            {
                var path = Path.Combine(directory, fileName);
                var generated = new MigrationFile(path, session.RelativePath(path), provider, newLine);
                session.GeneratedFiles[path] = content;
                session.Marked(Id, generated, 1, message);
                session.Changed(Id, generated, 0);
            }

            if (!session.Registrations.TryGetValue(provider, out var registrations))
            {
                registrations = [];
                session.Registrations[provider] = registrations;
            }

            registrations.Add(new V4ServiceRegistration(ns, "CurrentUnitOfWorkAccessor", "Singleton", [Accessor]));
        }
    }

    /// <inheritdoc />
    public override string Apply(MigrationFile file, string text, MigrationSession session)
        => file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? text : base.Apply(file, text, session);

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        var rewriter = new Rewriter(this, file, session);
        var result = (CompilationUnitSyntax)rewriter.Visit(root);
        if (rewriter.Usings.Count == 0)
        {
            return result;
        }

        // 消费类与访问器同命名空间（或在其下）时不用补 using
        var namespaces = result.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Select(static declaration => declaration.Name.ToString()).ToList();
        var missing = rewriter.Usings.Where(ns => !namespaces.Any(declared => declared == ns || declared.StartsWith(ns + ".", StringComparison.Ordinal))).ToList();
        return missing.Count == 0 ? result : WithUsings(result, missing, file.NewLine);
    }

    /// <summary>
    /// 类是否以字段、属性、构造函数参数或主构造函数参数持有 <c>IUnitOfWorkManager</c>。
    /// </summary>
    private static bool Consumes(ClassDeclarationSyntax declaration)
    {
        var parameters = declaration.ParameterList?.Parameters.Select(static parameter => parameter.Type)
            .Concat(declaration.Members.OfType<ConstructorDeclarationSyntax>().SelectMany(static constructor => constructor.ParameterList.Parameters).Select(static parameter => parameter.Type)) ?? [];
        var members = declaration.Members.OfType<FieldDeclarationSyntax>().Select(static field => field.Declaration.Type)
            .Concat(declaration.Members.OfType<PropertyDeclarationSyntax>().Select(static property => property.Type));
        return parameters.Concat(members).Any(static type => type is not null && SimpleName(type) == Manager);
    }

    /// <summary>
    /// 是否对工作单元管理器的 <c>Begin</c> 调用（按接收者名字判断，语法级没有类型信息）。
    /// </summary>
    private static bool BeginsUnitOfWork(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Begin" } access
            && access.Expression.ToString().Contains("UnitOfWorkManager", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 逐成员删除特性、把单例消费类的 <c>IUnitOfWorkManager</c> 换成访问器，并对其余工作单元管理器留标记。
    /// </summary>
    private sealed class Rewriter(UnitOfWorkFixer fixer, MigrationFile file, MigrationSession session) : CSharpSyntaxRewriter
    {
        /// <summary>
        /// 改写后要补的 using（生成访问器的命名空间）。
        /// </summary>
        public List<string> Usings { get; } = [];

        /// <inheritdoc />
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) => Strip((MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!, LineOf(node));

        /// <inheritdoc />
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var line = LineOf(node);
            if (session.UnitOfWorkAccessorConsumers.TryGetValue(node.Identifier.ValueText, out var ns))
            {
                // 先把类型换掉，后面的成员访问就不会再给它留标记
                var renamer = new ManagerRenamer();
                var renamed = (ClassDeclarationSyntax)renamer.Visit(node);
                for (var count = 0; count < renamer.Count; count++)
                {
                    session.Changed(fixer.Id, file, line);
                }

                if (renamer.Count > 0 && !Usings.Contains(ns))
                {
                    Usings.Add(ns);
                }

                node = renamed;
            }

            return Strip((ClassDeclarationSyntax)base.VisitClassDeclaration(node)!, line);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => Strip((InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!, LineOf(node));

        /// <inheritdoc />
        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
            => SimpleName(node.Declaration.Type) == Manager ? Mark(node, LineOf(node)) : base.VisitFieldDeclaration(node);

        /// <inheritdoc />
        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
            => node.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(BeginsUnitOfWork) ? Mark(node, LineOf(node)) : base.VisitLocalDeclarationStatement(node);

        /// <inheritdoc />
        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
            => node.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(BeginsUnitOfWork) ? Mark(node, LineOf(node)) : base.VisitExpressionStatement(node);

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

    /// <summary>
    /// 把一个类里的 <c>IUnitOfWorkManager</c>（含限定名写法）换成 <c>ICurrentUnitOfWorkAccessor</c>。
    /// </summary>
    private sealed class ManagerRenamer : CSharpSyntaxRewriter
    {
        /// <summary>
        /// 换掉的个数。
        /// </summary>
        public int Count { get; private set; }

        /// <inheritdoc />
        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
        {
            // XiHan.Framework.Uow.IUnitOfWorkManager → 简单名（访问器在应用自己的命名空间里，由 using 引入）
            if (node.Right is IdentifierNameSyntax { Identifier.ValueText: Manager })
            {
                Count++;
                return SyntaxFactory.IdentifierName(Accessor).WithTriviaFrom(node);
            }

            return base.VisitQualifiedName(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.ValueText == Manager)
            {
                Count++;
                return node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, Accessor, node.Identifier.TrailingTrivia));
            }

            return base.VisitIdentifierName(node);
        }
    }
}
