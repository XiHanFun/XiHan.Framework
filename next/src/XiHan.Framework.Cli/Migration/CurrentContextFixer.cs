// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-07：不改 <c>ICurrentUser</c> / <c>ICurrentTenant</c> 的调用点。每条项目引用链最底层的使用项目生成同名应用接口与读 <c>IInvocationAccessor.Current</c> 的实现（<c>Security/</c> 目录，命名空间 <c>&lt;项目&gt;.Security</c>），
/// 使用文件补 <c>using</c>；生成的 <c>CurrentTenant.Change(...)</c> 用 AsyncLocal 覆盖栈实现，调用点不动。项目文件只在第一遍读引用图，不改写。
/// </summary>
internal sealed partial class CurrentContextFixer : SyntaxFixer
{
    /// <summary>
    /// 4.x 的两个接口名。
    /// </summary>
    private static readonly HashSet<string> _interfaces = new(StringComparer.Ordinal) { "ICurrentUser", "ICurrentTenant" };

    /// <inheritdoc />
    public override string Id => "V4-07";

    /// <inheritdoc />
    public override bool Handles(MigrationFile file) => base.Handles(file) || file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override void Scan(MigrationFile file, string text, MigrationSession session)
    {
        if (file.ProjectDirectory is null)
        {
            return;
        }

        if (file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            // 项目引用图：Include 相对项目文件所在目录
            if (!session.ProjectReferences.TryGetValue(file.ProjectDirectory, out var references))
            {
                references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                session.ProjectReferences[file.ProjectDirectory] = references;
            }

            foreach (Match match in ProjectReference().Matches(text))
            {
                var include = match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                if (Path.GetDirectoryName(Path.GetFullPath(Path.Combine(file.ProjectDirectory, include))) is { } directory)
                {
                    references.Add(directory);
                }
            }

            return;
        }

        if (Uses(CSharpSyntaxTree.ParseText(text, path: file.Path).GetCompilationUnitRoot()))
        {
            session.CurrentContextUsers.Add(file.ProjectDirectory);
        }
    }

    /// <inheritdoc />
    public override void Prepare(MigrationSession session)
    {
        foreach (var user in session.CurrentContextUsers.OrderBy(static user => user, StringComparer.Ordinal))
        {
            // 候选：自己与引用闭包里的使用项目；提供者是其中不再引用别的候选的那个（最底层）
            var closure = Closure(user, session.ProjectReferences);
            var candidates = closure.Where(session.CurrentContextUsers.Contains).Append(user).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var provider = candidates
                .Where(candidate => !Closure(candidate, session.ProjectReferences).Any(reference => candidates.Contains(reference, StringComparer.OrdinalIgnoreCase)))
                .OrderBy(static candidate => candidate, StringComparer.Ordinal)
                .FirstOrDefault() ?? user;
            session.CurrentContextProviders[user] = provider;
        }

        foreach (var provider in session.CurrentContextProviders.Values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static provider => provider, StringComparer.Ordinal))
        {
            var directory = Path.Combine(provider, "Security");
            // 已生成过（再跑一遍）就不动
            if (File.Exists(Path.Combine(directory, V4CurrentContextGenerator.FileNames[0])))
            {
                continue;
            }

            var (project, _) = V4ServicesGenerator.Names(provider);
            var newLine = session.ProjectNewLine(provider);
            foreach (var (name, content) in V4CurrentContextGenerator.Generate(project + ".Security", newLine))
            {
                var path = Path.Combine(directory, name);
                session.GeneratedFiles[path] = content;
                session.Changed(Id, new MigrationFile(path, session.RelativePath(path), provider, newLine), 0);
            }

            if (!session.Registrations.TryGetValue(provider, out var registrations))
            {
                registrations = [];
                session.Registrations[provider] = registrations;
            }

            registrations.Add(new V4ServiceRegistration(project + ".Security", "CurrentUser", "Scoped", ["ICurrentUser"]));
            registrations.Add(new V4ServiceRegistration(project + ".Security", "CurrentTenant", "Scoped", ["ICurrentTenant"]));
        }
    }

    /// <inheritdoc />
    public override string Apply(MigrationFile file, string text, MigrationSession session)
        => file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? text : base.Apply(file, text, session);

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        if (file.ProjectDirectory is null || !session.CurrentContextProviders.TryGetValue(file.ProjectDirectory, out var provider) || !Uses(root))
        {
            return root;
        }

        // 只补 using：生成的 CurrentTenant 自带 Change（AsyncLocal 覆盖栈），调用点不用改
        var ns = V4ServicesGenerator.Names(provider).Project + ".Security";
        if (root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Any(declaration => declaration.Name.ToString() == ns))
        {
            return root;
        }

        var withUsing = WithUsings(root, [ns], file.NewLine);
        if (!ReferenceEquals(withUsing, root))
        {
            session.Changed(Id, file, 1);
        }

        return withUsing;
    }

    /// <summary>
    /// 文件里是否用到两个接口名。
    /// </summary>
    private static bool Uses(CompilationUnitSyntax root)
        => root.DescendantNodes().OfType<IdentifierNameSyntax>().Any(static name => _interfaces.Contains(name.Identifier.ValueText));

    /// <summary>
    /// 项目的传递引用闭包（不含自己）。
    /// </summary>
    private static HashSet<string> Closure(string project, Dictionary<string, HashSet<string>> references)
    {
        var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>([project]);
        while (queue.TryDequeue(out var current))
        {
            if (!references.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var reference in next)
            {
                if (closure.Add(reference))
                {
                    queue.Enqueue(reference);
                }
            }
        }

        closure.Remove(project);
        return closure;
    }

    /// <summary>
    /// 单行的 <c>&lt;ProjectReference Include="…" /&gt;</c>。
    /// </summary>
    [GeneratedRegex("<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"")]
    private static partial Regex ProjectReference();
}
