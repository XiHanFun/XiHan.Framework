// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-03：删除 <c>ITransientDependency</c> / <c>IScopedDependency</c> / <c>ISingletonDependency</c> 与 <c>[Dependency]</c>、<c>[ExposeServices]</c>，
/// 把推出的注册项记进会话；自己没有标记、沿基类链继承到标记或 4.x 框架基类（<see cref="V4ServiceBases"/>）的具体类，按继承到的存期注册类本身与默认暴露的接口；
/// 运行结束时生成 <c>&lt;项目&gt;V4Services.cs</c>。
/// </summary>
internal sealed class DependencyMarkerFixer : SyntaxFixer
{
    /// <summary>
    /// 标记接口 → 存期。
    /// </summary>
    private static readonly Dictionary<string, string> _lifetimes = new(StringComparer.Ordinal)
    {
        ["ITransientDependency"] = "Transient",
        ["IScopedDependency"] = "Scoped",
        ["ISingletonDependency"] = "Singleton",
    };

    /// <summary>
    /// 不算服务契约的常见接口：实现它们不代表要按它们注册。
    /// </summary>
    private static readonly HashSet<string> _notServices = new(StringComparer.Ordinal)
    {
        "IDisposable", "IAsyncDisposable", "IValidatableObject", "IEquatable", "IComparable", "IEnumerable", "INotifyPropertyChanged", "ICloneable",
    };

    /// <inheritdoc />
    public override string Id => "V4-03";

    /// <inheritdoc />
    public override void Scan(MigrationFile file, string text, MigrationSession session)
    {
        var root = CSharpSyntaxTree.ParseText(text, path: file.Path).GetCompilationUnitRoot();
        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var bases = declaration.BaseList?.Types.Select(static type => SimpleName(type.Type)).ToList() ?? [];
            var markers = bases.Where(_lifetimes.ContainsKey).Select(name => _lifetimes[name]).ToList();
            var dependency = declaration.AttributeLists.SelectMany(static list => list.Attributes).FirstOrDefault(static attribute => AttributeName(attribute) == "Dependency");
            var declared = dependency is not null && TryLifetime(dependency, out var attributed) ? attributed : markers.Count > 0 ? Prefer(markers) : null;
            session.Services.Describe(
                declaration.Identifier.ValueText,
                bases,
                declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty,
                file.ProjectDirectory,
                declaration.TypeParameterList?.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToList() ?? [],
                declaration.BaseList?.Types.Select(static type => type.Type).Where(static type => IsInterfaceLike(SimpleName(type))).Select(static type => type.ToString()).ToList() ?? [],
                declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) || declaration.Modifiers.Any(SyntaxKind.StaticKeyword),
                declared,
                dependency is not null && declared is not null);
        }
    }

    /// <inheritdoc />
    public override void Prepare(MigrationSession session)
    {
        // 自己没有标记的具体类：沿基类链继承到的存期；动态 API 服务类由 V4-06 按作用域登记，不重复
        foreach (var name in session.Services.InheritedRegistrations())
        {
            if (session.Services.IsDynamicApi(name))
            {
                continue;
            }

            var (ns, projectDirectory, services) = session.Services.Registration(name);
            if (!session.Registrations.TryGetValue(projectDirectory, out var registrations))
            {
                registrations = [];
                session.Registrations[projectDirectory] = registrations;
            }

            registrations.Add(new V4ServiceRegistration(ns, name, session.Services.InheritedLifetime(name)!, services, IncludeSelf: true));
        }
    }

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
        => (CompilationUnitSyntax)new Rewriter(this, file, session).Visit(root);

    /// <summary>
    /// 简单名是否像接口（<c>I</c> + 大写字母开头），且不是标记接口或 <c>IDisposable</c> 等非契约接口。
    /// </summary>
    private static bool IsInterfaceLike(string name) => name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) && !_lifetimes.ContainsKey(name) && !_notServices.Contains(name);

    /// <summary>
    /// 多个标记接口并存时按 4.x 的优先级取：Transient、Singleton、Scoped。
    /// </summary>
    private static string Prefer(IReadOnlyList<string> lifetimes)
        => lifetimes.Contains("Transient") ? "Transient" : lifetimes.Contains("Singleton") ? "Singleton" : "Scoped";

    /// <summary>
    /// 从 <c>[Dependency(ServiceLifetime.Scoped)]</c> 读存期。
    /// </summary>
    private static bool TryLifetime(AttributeSyntax attribute, out string lifetime)
    {
        var text = attribute.ArgumentList?.ToString() ?? string.Empty;
        foreach (var candidate in _lifetimes.Values)
        {
            if (text.Contains(candidate, StringComparison.Ordinal))
            {
                lifetime = candidate;
                return true;
            }
        }

        lifetime = string.Empty;
        return false;
    }

    /// <summary>
    /// 逐类处理。
    /// </summary>
    private sealed class Rewriter(DependencyMarkerFixer fixer, MigrationFile file, MigrationSession session) : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            var markers = visited.BaseList?.Types.Where(static t => _lifetimes.ContainsKey(SimpleName(t.Type))).ToList() ?? [];
            var dependency = visited.AttributeLists.SelectMany(static l => l.Attributes).FirstOrDefault(static a => AttributeName(a) == "Dependency");
            if (markers.Count == 0 && dependency is null)
            {
                return visited;
            }

            var line = LineOf(node);
            // 存期：[Dependency(ServiceLifetime.X)] 优先于标记接口；多个标记接口按 Transient、Singleton、Scoped
            var lifetime = dependency is not null && TryLifetime(dependency, out var declared) ? declared : Prefer(markers.Select(static m => _lifetimes[SimpleName(m.Type)]).ToList());
            var exposed = visited.AttributeLists.SelectMany(static l => l.Attributes).FirstOrDefault(static a => AttributeName(a) == "ExposeServices");
            var services = exposed is not null
                ? exposed.ArgumentList?.Arguments.Select(static a => a.Expression).OfType<TypeOfExpressionSyntax>().Select(static t => t.Type.ToString()).ToList() ?? []
                : visited.BaseList!.Types.Select(static t => SimpleName(t.Type)).Where(IsInterfaceLike).ToList();

            if (visited.TypeParameterList is null && !visited.Modifiers.Any(SyntaxKind.AbstractKeyword) && file.ProjectDirectory is not null)
            {
                var ns = visited.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;
                if (!session.Registrations.TryGetValue(file.ProjectDirectory, out var registrations))
                {
                    registrations = [];
                    session.Registrations[file.ProjectDirectory] = registrations;
                }

                registrations.Add(new V4ServiceRegistration(ns, visited.Identifier.ValueText, lifetime, services));
            }
            else if (!visited.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {
                // 泛型或不在任何项目目录下的类没法写进生成文件，留给人；抽象类本身不注册，它的具体派生类沿基类链继承存期（Prepare 已登记）
                var message = CliMessages.MigrateMarkerManualRegistration.L(("name", visited.Identifier.ValueText));
                session.Marked(fixer.Id, file, line, message);
                visited = Markers.With(visited, fixer.Id, message, file.NewLine);
            }

            session.Changed(fixer.Id, file, line);
            visited = RemoveAttributes(visited, static name => name is "Dependency" or "ExposeServices", out _);
            var names = markers.Select(static m => SimpleName(m.Type)).ToHashSet(StringComparer.Ordinal);
            return RemoveBaseTypes(visited, names.Contains);
        }
    }
}
