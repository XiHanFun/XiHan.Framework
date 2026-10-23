// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-03：删除 <c>ITransientDependency</c> / <c>IScopedDependency</c> / <c>ISingletonDependency</c> 与 <c>[Dependency]</c>、<c>[ExposeServices]</c>，
/// 把推出的注册项记进会话，运行结束时生成 <c>&lt;项目&gt;V4Services.cs</c>。
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
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
        => (CompilationUnitSyntax)new Rewriter(this, file, session).Visit(root);

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
            // 存期：[Dependency(ServiceLifetime.X)] 优先于标记接口
            var lifetime = dependency is not null && TryLifetime(dependency, out var declared) ? declared : _lifetimes[SimpleName(markers[0].Type)];
            var exposed = visited.AttributeLists.SelectMany(static l => l.Attributes).FirstOrDefault(static a => AttributeName(a) == "ExposeServices");
            var services = exposed is not null
                ? exposed.ArgumentList?.Arguments.Select(static a => a.Expression).OfType<TypeOfExpressionSyntax>().Select(static t => t.Type.ToString()).ToList() ?? []
                : visited.BaseList!.Types.Select(static t => SimpleName(t.Type)).Where(static n => n.Length > 1 && n[0] == 'I' && char.IsUpper(n[1]) && !_lifetimes.ContainsKey(n) && !_notServices.Contains(n)).ToList();

            // 泛型、抽象或不在任何项目目录下的类没法写进生成文件，留给人
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
            else
            {
                var message = CliMessages.MigrateMarkerManualRegistration.L(("name", visited.Identifier.ValueText));
                session.Marked(fixer.Id, file, line, message);
                visited = Markers.With(visited, fixer.Id, message, file.NewLine);
            }

            session.Changed(fixer.Id, file, line);
            visited = RemoveAttributes(visited, static name => name is "Dependency" or "ExposeServices", out _);
            var names = markers.Select(static m => SimpleName(m.Type)).ToHashSet(StringComparer.Ordinal);
            return RemoveBaseTypes(visited, names.Contains);
        }

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
    }
}
