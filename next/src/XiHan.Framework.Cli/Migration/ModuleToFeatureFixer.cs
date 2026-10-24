// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-01：<c>XiHanXxxModule : XiHanModule</c> → <c>sealed XxxFeature : IFeature</c>（有初始化或关闭钩子则 <c>IHostedFeature</c>）：
/// 生成清单、按 Pre → 主 → Post 合并钩子体、<c>[DependsOn]</c> 映射成 <c>Requires</c>、本文件里的旧类名引用与文件名同步改名。
/// </summary>
internal sealed class ModuleToFeatureFixer : SyntaxFixer
{
    /// <summary>
    /// 服务配置钩子，按合并顺序。
    /// </summary>
    private static readonly string[] _configureHooks = ["PreConfigureServices", "ConfigureServices", "PostConfigureServices"];

    /// <summary>
    /// 初始化钩子，按合并顺序。
    /// </summary>
    private static readonly string[] _startHooks = ["OnPreApplicationInitialization", "OnApplicationInitialization", "OnPostApplicationInitialization"];

    /// <summary>
    /// 关闭钩子。
    /// </summary>
    private static readonly string[] _stopHooks = ["OnApplicationShutdown"];

    /// <summary>
    /// 生成的特性类总要引入的命名空间。
    /// </summary>
    private static readonly string[] _usings = ["XiHan.Framework.Kernel", "XiHan.Framework.Kernel.Hosting", "XiHan.Framework.Primitives", "XiHan.Framework.Primitives.Manifests"];

    /// <inheritdoc />
    public override string Id => "V4-01";

    /// <inheritdoc />
    public override void Scan(MigrationFile file, string text, MigrationSession session)
    {
        // 范围内的 IServiceCollection 扩展方法：应用已承接的 4.x 模块注册（AddXiHanDistributedIds 等）可以直接生成调用
        var root = CSharpSyntaxTree.ParseText(text, path: file.Path).GetCompilationUnitRoot();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!method.Modifiers.Any(SyntaxKind.StaticKeyword) || method.ParameterList.Parameters.Count == 0)
            {
                continue;
            }

            var first = method.ParameterList.Parameters[0];
            if (!first.Modifiers.Any(SyntaxKind.ThisKeyword) || first.Type is null || SimpleName(first.Type) != "IServiceCollection")
            {
                continue;
            }

            var configuration = method.ParameterList.Parameters.Skip(1).Any(static parameter => parameter.Type is not null && SimpleName(parameter.Type) == "IConfiguration");
            session.ServiceExtensions[method.Identifier.ValueText] = session.ServiceExtensions.GetValueOrDefault(method.Identifier.ValueText) || configuration;
        }
    }

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        var modules = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(IsModule).ToList();
        if (modules.Count == 0)
        {
            return root;
        }

        var usings = new HashSet<string>(_usings, StringComparer.Ordinal);
        var converted = modules.ToDictionary(static module => module, module => Convert(module, file, session, usings));
        var result = root.ReplaceNodes(converted.Keys, (original, _) => converted[original]);
        return WithUsings(result, usings, file.NewLine);
    }

    /// <summary>
    /// 是否 4.x 模块类。
    /// </summary>
    private static bool IsModule(ClassDeclarationSyntax node) => node.BaseList?.Types.Any(static type => SimpleName(type.Type) == "XiHanModule") == true;

    /// <summary>
    /// 特性类名：去掉 <c>XiHan</c> 前缀，<c>Module</c> 换 <c>Feature</c>。
    /// </summary>
    private static string FeatureName(string moduleName)
    {
        var stem = moduleName;
        if (stem.StartsWith("XiHan", StringComparison.Ordinal))
        {
            stem = stem["XiHan".Length..];
        }

        if (stem.EndsWith("Module", StringComparison.Ordinal))
        {
            stem = stem[..^"Module".Length];
        }

        return (stem.Length > 0 ? stem : moduleName) + "Feature";
    }

    /// <summary>
    /// 清单 ID：命名空间小写、去掉 <c>xihan</c> 头，第二段之后用 <c>-</c> 连成第三段（清单文法只允许三段）。
    /// </summary>
    private static string ManifestId(string ns)
    {
        var segments = ns.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(Slug).Where(static s => s.Length > 0).ToList();
        if (segments.Count > 0 && segments[0] == "xihan")
        {
            segments.RemoveAt(0);
        }

        return segments.Count switch
        {
            0 => "xihan.app.feature",
            1 => $"xihan.app.{segments[0]}",
            _ => $"xihan.{segments[0]}.{string.Join('-', segments.Skip(1))}",
        };
    }

    /// <summary>
    /// 段落只留小写字母与数字，且以字母开头。
    /// </summary>
    private static string Slug(string segment)
    {
        var letters = new string(segment.ToLowerInvariant().Where(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9').ToArray());
        return letters.Length > 0 && char.IsAsciiDigit(letters[0]) ? "x" + letters : letters;
    }

    /// <summary>
    /// 把一个模块类改成特性类。
    /// </summary>
    private ClassDeclarationSyntax Convert(ClassDeclarationSyntax module, MigrationFile file, MigrationSession session, HashSet<string> usings)
    {
        var oldName = module.Identifier.ValueText;
        var newName = FeatureName(oldName);
        var ns = module.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;
        var indent = IndentOf(module) + "    ";
        var newLine = file.NewLine;
        session.Changed(Id, file, LineOf(module));

        // [DependsOn]：框架模块映射成能力，无能力的记下来，应用模块直接删；被删的框架模块按出现顺序记下来，补它们丢失的注册
        var requires = new List<string> { V4Modules.Hosting };
        var dropped = new List<string>();
        var frameworkModules = new List<string>();
        foreach (var attribute in module.AttributeLists.SelectMany(static list => list.Attributes).Where(static attribute => AttributeName(attribute) == "DependsOn"))
        {
            session.Changed(Id, file, LineOf(attribute));
            foreach (var typeOf in attribute.ArgumentList?.Arguments.Select(static argument => argument.Expression).OfType<TypeOfExpressionSyntax>() ?? [])
            {
                var name = SimpleName(typeOf.Type);
                if (!V4Modules.TryGetCapability(name, out var capability))
                {
                    continue;
                }

                if (!frameworkModules.Contains(name))
                {
                    frameworkModules.Add(name);
                }

                if (capability is null)
                {
                    dropped.Add(name);
                }
                else if (!requires.Contains(capability))
                {
                    requires.Add(capability);
                }
            }
        }

        var methods = module.Members.OfType<MethodDeclarationSyntax>().ToList();
        var configure = Hooks(methods, _configureHooks);
        var start = Hooks(methods, _startHooks);
        var stop = Hooks(methods, _stopHooks);
        var removed = new HashSet<MemberDeclarationSyntax>(configure.Concat(start).Concat(stop));
        foreach (var hook in removed)
        {
            session.Changed(Id, file, LineOf(hook));
        }

        var configureBodies = configure.Select(hook => Statements(hook, HookKind.Configure, indent, file, session)).ToList();
        var startBodies = start.Select(hook => Statements(hook, HookKind.Start, indent, file, session)).ToList();
        var stopBodies = stop.Select(hook => Statements(hook, HookKind.Stop, indent, file, session)).ToList();
        // 初始化或关闭钩子去掉空壳后还有语句的才是宿主特性
        var hosted = startBodies.Any(static body => body.Count > 0) || stopBodies.Any(static body => body.Count > 0);
        var generated = new StringBuilder();
        generated.Append(ManifestText(newName, ns, requires, indent, newLine, usings));
        generated.Append(newLine).Append(MethodText(indent, newLine, "public void Configure(FeatureContext context)", configureBodies, null, ModuleRegistrations(frameworkModules, configure, module, file, session)));
        if (hosted)
        {
            var startAsync = start.Any(IsAsync);
            generated.Append(newLine).Append(MethodText(indent, newLine, $"public {(startAsync ? "async " : string.Empty)}Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)", startBodies, startAsync ? null : "return Task.CompletedTask;"));
            var stopAsync = stop.Any(IsAsync);
            generated.Append(newLine).Append(MethodText(indent, newLine, $"public {(stopAsync ? "async " : string.Empty)}Task StopAsync(CancellationToken cancellationToken)", stopBodies, stopAsync ? null : "return Task.CompletedTask;"));
        }

        var members = ParseMembers(generated.ToString(), newLine).ToList();
        var kept = new List<MemberDeclarationSyntax>();
        foreach (var member in module.Members)
        {
            if (removed.Contains(member))
            {
                continue;
            }

            // SkipAutoServiceRegistration：5.0 没有约定注册，属性重写直接删
            if (member is PropertyDeclarationSyntax { Identifier.ValueText: "SkipAutoServiceRegistration" })
            {
                session.Changed(Id, file, LineOf(member));
                continue;
            }

            kept.Add(member);
        }

        if (kept.Count > 0 && !StartsWithBlankLine(kept[0]))
        {
            // 生成成员与保留成员之间空一行
            members[^1] = members[^1].WithTrailingTrivia(members[^1].GetTrailingTrivia().Add(SyntaxFactory.EndOfLine(newLine)));
        }

        members.AddRange(kept);
        var baseTypes = module.BaseList!.Types.Select(type => SimpleName(type.Type) == "XiHanModule"
            ? type.WithType(SyntaxFactory.IdentifierName(hosted ? "IHostedFeature" : "IFeature").WithTriviaFrom(type.Type))
            : type);
        var result = module
            .WithIdentifier(SyntaxFactory.Identifier(module.Identifier.LeadingTrivia, newName, module.Identifier.TrailingTrivia))
            .WithBaseList(module.BaseList.WithTypes(SyntaxFactory.SeparatedList(baseTypes, module.BaseList.Types.GetSeparators())))
            .WithMembers(SyntaxFactory.List(members));
        result = RemoveAttributes(result, static name => name == "DependsOn", out _);
        result = Seal(result);
        if (dropped.Count > 0)
        {
            var message = CliMessages.MigrateMarkerModulesDropped.L(("modules", string.Join(", ", dropped)));
            session.Marked(Id, file, LineOf(module), message);
            result = Markers.With(result, Id, message, newLine);
        }

        // 本文件里对旧类名的引用（typeof、nameof、ILogger<T>）跟着改名
        result = (ClassDeclarationSyntax)new RenameRewriter(oldName, newName).Visit(result)!;
        if (string.Equals(Path.GetFileNameWithoutExtension(file.Path), oldName, StringComparison.Ordinal))
        {
            session.Renames[file.Path] = Path.Combine(Path.GetDirectoryName(file.Path)!, newName + ".cs");
        }

        return result;
    }

    /// <summary>
    /// 按名字顺序取钩子方法：每个名字先同步后 <c>Async</c> 变体。
    /// </summary>
    private static List<MethodDeclarationSyntax> Hooks(List<MethodDeclarationSyntax> methods, string[] names)
    {
        var hooks = new List<MethodDeclarationSyntax>();
        foreach (var name in names)
        {
            hooks.AddRange(methods.Where(method => method.Identifier.ValueText == name));
            hooks.AddRange(methods.Where(method => method.Identifier.ValueText == name + "Async"));
        }

        return hooks;
    }

    /// <summary>
    /// 是否 <c>async</c> 方法。
    /// </summary>
    private static bool IsAsync(MethodDeclarationSyntax method) => method.Modifiers.Any(SyntaxKind.AsyncKeyword);

    /// <summary>
    /// 清单属性源码。
    /// </summary>
    private static string ManifestText(string featureName, string ns, IReadOnlyList<string> requires, string indent, string newLine, HashSet<string> usings)
    {
        var builder = new StringBuilder();
        builder.Append(indent).Append("/// <inheritdoc />").Append(newLine);
        builder.Append(indent).Append("public Manifest Manifest { get; } = new()").Append(newLine);
        builder.Append(indent).Append('{').Append(newLine);
        builder.Append(indent).Append("    Id = \"").Append(ManifestId(ns)).Append("\",").Append(newLine);
        if (ns.Length > 0)
        {
            builder.Append(indent).Append("    Package = \"").Append(ns).Append("\",").Append(newLine);
        }

        builder.Append(indent).Append("    Version = \"1.0.0\",").Append(newLine);
        builder.Append(indent).Append("    Primitives = 1,").Append(newLine);
        builder.Append(indent).Append("    Stability = Stability.Stable,").Append(newLine);
        builder.Append(indent).Append("    Kind = ManifestKind.Capability,").Append(newLine);
        builder.Append(indent).Append("    Requires =").Append(newLine);
        builder.Append(indent).Append("    [").Append(newLine);
        foreach (var capability in requires)
        {
            var (type, member, @namespace) = V4Modules.Reference(capability);
            usings.Add(@namespace);
            builder.Append(indent).Append("        new CapabilityRequirement(").Append(type).Append('.').Append(member).Append(", ").Append(type).Append(".Version),").Append(newLine);
        }

        builder.Append(indent).Append("    ],").Append(newLine);
        builder.Append(indent).Append("    Entry = typeof(").Append(featureName).Append(").FullName,").Append(newLine);
        builder.Append(indent).Append("};").Append(newLine);
        return builder.ToString();
    }

    /// <summary>
    /// 被删的 4.x 框架模块在 <c>ConfigureServices</c> 里做过的注册（§4.1）：应用已有同名承接扩展方法、且原钩子体里还没调用的直接生成调用；
    /// 原钩子体里已调用的不再提；其余按模块各留一条带清单的标记，让人按承接代码的实际依赖补回。返回要追加在 <c>Configure</c> 末尾的行。
    /// </summary>
    private List<string> ModuleRegistrations(IReadOnlyList<string> frameworkModules, IReadOnlyList<MethodDeclarationSyntax> configureHooks, ClassDeclarationSyntax module, MigrationFile file, MigrationSession session)
    {
        var invoked = configureHooks
            .SelectMany(static hook => hook.DescendantNodes().OfType<InvocationExpressionSyntax>())
            .Select(static invocation => invocation.Expression switch
            {
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                GenericNameSyntax generic => generic.Identifier.ValueText,
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                _ => string.Empty,
            })
            .ToHashSet(StringComparer.Ordinal);
        var calls = new List<string>();
        var markers = new List<string>();
        foreach (var moduleName in frameworkModules)
        {
            var remaining = new List<string>();
            foreach (var registration in V4Modules.Registrations(moduleName))
            {
                if (registration.Method is not null && invoked.Contains(registration.Method))
                {
                    continue;
                }

                if (registration.Method is not null && session.ServiceExtensions.TryGetValue(registration.Method, out var configuration))
                {
                    var call = $"context.Services.{registration.Method}({(configuration && registration.Configuration ? "context.Configuration" : string.Empty)});";
                    if (!calls.Contains(call))
                    {
                        calls.Add(call);
                    }

                    continue;
                }

                remaining.Add(registration.ToString());
            }

            if (remaining.Count > 0)
            {
                var message = CliMessages.MigrateMarkerModuleRegistrations.L(("module", moduleName), ("registrations", string.Join("; ", remaining)));
                session.Marked(Id, file, LineOf(module), message);
                markers.Add(Markers.Line(Id, message));
            }
        }

        foreach (var call in calls)
        {
            session.Changed(Id, file, LineOf(module));
        }

        return [.. calls, .. markers];
    }

    /// <summary>
    /// 方法源码：各钩子体按顺序拼接，之间空一行；<paramref name="tail"/> 是末尾补的语句；<paramref name="trailing"/> 是再往后追加的行（生成的承接调用与标记），与前面的内容空一行。
    /// </summary>
    private static string MethodText(string indent, string newLine, string signature, IEnumerable<List<StatementSyntax>> bodies, string? tail, IReadOnlyList<string>? trailing = null)
    {
        var builder = new StringBuilder();
        builder.Append(indent).Append("/// <inheritdoc />").Append(newLine);
        builder.Append(indent).Append(signature).Append(newLine);
        builder.Append(indent).Append('{').Append(newLine);
        var first = true;
        foreach (var body in bodies.Where(static body => body.Count > 0))
        {
            if (!first)
            {
                builder.Append(newLine);
            }

            first = false;
            var text = SyntaxFactory.List(body).ToFullString();
            builder.Append(text);
            if (!text.EndsWith('\n'))
            {
                builder.Append(newLine);
            }
        }

        if (tail is not null)
        {
            if (!first)
            {
                builder.Append(newLine);
            }

            first = false;
            builder.Append(indent).Append("    ").Append(tail).Append(newLine);
        }

        if (trailing is { Count: > 0 })
        {
            if (!first)
            {
                builder.Append(newLine);
            }

            foreach (var line in trailing)
            {
                builder.Append(indent).Append("    ").Append(line).Append(newLine);
            }
        }

        builder.Append(indent).Append('}').Append(newLine);
        return builder.ToString();
    }

    /// <summary>
    /// 一个钩子的语句列表：先在原语句上留标记（行号取原文件），再按钩子种类改写；末尾的 <c>return Task.CompletedTask;</c> 去掉。
    /// </summary>
    private List<StatementSyntax> Statements(MethodDeclarationSyntax hook, HookKind kind, string indent, MigrationFile file, MigrationSession session)
    {
        var body = hook.Body;
        if (body is null && hook.ExpressionBody is not null)
        {
            var expression = hook.ExpressionBody.Expression;
            if (expression.ToString() == "Task.CompletedTask")
            {
                return [];
            }

            body = SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(expression.WithoutTrivia())
                .WithLeadingTrivia(SyntaxFactory.Whitespace(indent + "    "))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine(file.NewLine)));
        }

        if (body is null)
        {
            return [];
        }

        var statements = body.Statements.ToList();
        if (statements.Count > 0 && statements[^1] is ReturnStatementSyntax { Expression: { } returned } && returned.ToString() == "Task.CompletedTask")
        {
            statements.RemoveAt(statements.Count - 1);
        }

        for (var index = 0; index < statements.Count; index++)
        {
            var statement = statements[index];
            switch (kind)
            {
                case HookKind.Configure when index == 0 && IsAsync(hook):
                    statements[index] = Mark(statement, CliMessages.MigrateMarkerAsyncConfigure.L(), file, session);
                    break;
                case HookKind.Start when statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(static invocation => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetApplicationBuilder" }):
                    statements[index] = Mark(statement, CliMessages.MigrateMarkerApplicationBuilder.L(), file, session);
                    break;
                case HookKind.Stop when statement.DescendantNodes().OfType<IdentifierNameSyntax>().Any(static name => name.Identifier.ValueText == "context"):
                    statements[index] = Mark(statement, CliMessages.MigrateMarkerShutdownContext.L(), file, session);
                    break;
            }
        }

        var block = body.WithStatements(SyntaxFactory.List(statements));
        block = kind switch
        {
            HookKind.Configure => (BlockSyntax)new ConfigureBodyRewriter(this, file, session).Visit(block),
            HookKind.Start => (BlockSyntax)new StartBodyRewriter(this, file, session).Visit(block),
            _ => block,
        };
        return block.Statements.ToList();
    }

    /// <summary>
    /// 留标记并记报告。
    /// </summary>
    private TNode Mark<TNode>(TNode node, string message, MigrationFile file, MigrationSession session)
        where TNode : SyntaxNode
    {
        if (Markers.Has(node, Id, message))
        {
            return node;
        }

        session.Marked(Id, file, LineOf(node), message);
        return Markers.With(node, Id, message, file.NewLine);
    }

    /// <summary>
    /// 加 <c>sealed</c>（抽象、静态或已 sealed 的不加）；放在 <c>partial</c> 之前。
    /// </summary>
    private static ClassDeclarationSyntax Seal(ClassDeclarationSyntax node)
    {
        if (node.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.SealedKeyword) || modifier.IsKind(SyntaxKind.AbstractKeyword) || modifier.IsKind(SyntaxKind.StaticKeyword)))
        {
            return node;
        }

        var sealedToken = SyntaxFactory.Token(SyntaxKind.SealedKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        if (node.Modifiers.Count == 0)
        {
            // 没有修饰符：sealed 接过 class 关键字的前导 trivia
            return node.WithModifiers(SyntaxFactory.TokenList(sealedToken.WithLeadingTrivia(node.Keyword.LeadingTrivia))).WithKeyword(node.Keyword.WithLeadingTrivia(SyntaxFactory.TriviaList()));
        }

        var partialIndex = node.Modifiers.IndexOf(SyntaxKind.PartialKeyword);
        return node.WithModifiers(partialIndex >= 0 ? node.Modifiers.Insert(partialIndex, sealedToken) : node.Modifiers.Add(sealedToken));
    }

    /// <summary>
    /// 钩子种类。
    /// </summary>
    private enum HookKind
    {
        /// <summary>服务配置。</summary>
        Configure,

        /// <summary>初始化。</summary>
        Start,

        /// <summary>关闭。</summary>
        Stop,
    }

    /// <summary>
    /// 服务配置钩子体：零参数的 <c>GetConfiguration()</c> → <c>context.Configuration</c>，基类的 <c>Configure&lt;T&gt;</c> / <c>PreConfigure&lt;T&gt;</c> / <c>PostConfigure&lt;T&gt;</c> 便捷方法 → <c>context.Services.Configure&lt;T&gt;</c> / <c>PostConfigure&lt;T&gt;</c>，删掉 <c>SkipAutoServiceRegistration</c> 赋值。
    /// </summary>
    private sealed class ConfigureBodyRewriter(ModuleToFeatureFixer fixer, MigrationFile file, MigrationSession session) : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            switch (visited.Expression)
            {
                case MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetConfiguration" } when visited.ArgumentList.Arguments.Count == 0:
                    session.Changed(fixer.Id, file, LineOf(node));
                    return SyntaxFactory.ParseExpression("context.Configuration").WithTriviaFrom(visited);
                case GenericNameSyntax { Identifier.ValueText: "Configure" or "PreConfigure" or "PostConfigure" } generic:
                    session.Changed(fixer.Id, file, LineOf(node));
                    var method = generic.Identifier.ValueText == "PostConfigure" ? "PostConfigure" : "Configure";
                    var target = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseExpression("context.Services"),
                        generic.WithIdentifier(SyntaxFactory.Identifier(method)).WithoutTrivia()).WithTriviaFrom(generic);
                    return visited.WithExpression(target);
                default:
                    return visited;
            }
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (node.Expression is AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "SkipAutoServiceRegistration" } })
            {
                session.Changed(fixer.Id, file, LineOf(node));
                return null;
            }

            return base.VisitExpressionStatement(node);
        }
    }

    /// <summary>
    /// 初始化钩子体：<c>context.ServiceProvider</c> → <c>services</c>；<c>var services = context.ServiceProvider;</c> 与参数同名，直接删掉。
    /// </summary>
    private sealed class StartBodyRewriter(ModuleToFeatureFixer fixer, MigrationFile file, MigrationSession session) : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (IsServiceProvider(node))
            {
                session.Changed(fixer.Id, file, LineOf(node));
                return SyntaxFactory.IdentifierName("services").WithTriviaFrom(node);
            }

            return base.VisitMemberAccessExpression(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            if (node.Declaration.Variables is [{ Identifier.ValueText: "services", Initializer.Value: MemberAccessExpressionSyntax access }] && IsServiceProvider(access))
            {
                session.Changed(fixer.Id, file, LineOf(node));
                return null;
            }

            return base.VisitLocalDeclarationStatement(node);
        }

        /// <summary>
        /// 是否 <c>context.ServiceProvider</c>。
        /// </summary>
        private static bool IsServiceProvider(MemberAccessExpressionSyntax node)
            => node is { Expression: IdentifierNameSyntax { Identifier.ValueText: "context" }, Name.Identifier.ValueText: "ServiceProvider" };
    }

    /// <summary>
    /// 把类内对旧类名的引用改成新名。
    /// </summary>
    private sealed class RenameRewriter(string oldName, string newName) : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
            => node.Identifier.ValueText == oldName ? node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, newName, node.Identifier.TrailingTrivia)) : base.VisitIdentifierName(node);

        /// <inheritdoc />
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
            return visited.Identifier.ValueText == oldName ? visited.WithIdentifier(SyntaxFactory.Identifier(visited.Identifier.LeadingTrivia, newName, visited.Identifier.TrailingTrivia)) : visited;
        }
    }
}
