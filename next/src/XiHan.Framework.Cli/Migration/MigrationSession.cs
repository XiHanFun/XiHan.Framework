// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一次迁移运行的共享状态：结果、跨文件累积的注册项、要改名的文件。
/// </summary>
internal sealed partial class MigrationSession
{
    /// <summary>
    /// 全部结果，按产生顺序。
    /// </summary>
    public List<MigrationFinding> Findings { get; } = [];

    /// <summary>
    /// V4-03 收集的注册项，按项目目录分组；运行结束时生成 <c>&lt;项目&gt;V4Services.cs</c>。
    /// </summary>
    public Dictionary<string, List<V4ServiceRegistration>> Registrations { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 要改名的文件：原绝对路径 → 新绝对路径（V4-01 模块类改名后文件同步改名）。
    /// </summary>
    public Dictionary<string, string> Renames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 第一遍扫描到的服务类目录（V4-06 判断 <c>[DynamicApi]</c> / <c>[Authorize]</c> 是否经基类继承）。
    /// </summary>
    public V4ServiceCatalog Services { get; } = new();

    /// <summary>
    /// 要写出的生成文件：绝对路径 → 文本（V4-06 的操作类、请求记录、空响应与序列化上下文）。
    /// </summary>
    public Dictionary<string, string> GeneratedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// V4-06 按项目目录汇总的操作生成信息。
    /// </summary>
    public Dictionary<string, V4OperationProject> OperationProjects { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 项目引用图：项目目录 → 它 <c>ProjectReference</c> 的项目目录（第一遍从 .csproj 读出）。
    /// </summary>
    public Dictionary<string, HashSet<string>> ProjectReferences { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 迁移范围内声明的 <c>IServiceCollection</c> 扩展方法：方法名 → 是否有 <c>IConfiguration</c> 参数（V4-01 据此直接生成被删模块注册的承接调用）。
    /// </summary>
    public Dictionary<string, bool> ServiceExtensions { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 含模块类或特性类（<c>XiHanModule</c> / <c>IFeature</c> / <c>IHostedFeature</c>）的文件，按项目目录分组（V4-01 第一遍记录；运行结束时往特性的 <c>Configure</c> 里插 <c>Add&lt;项目末段&gt;V4Services()</c> 调用）。
    /// </summary>
    public Dictionary<string, List<string>> FeatureFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 用到 <c>ICurrentUser</c> / <c>ICurrentTenant</c> / <c>ICurrentPrincipalAccessor</c> 的项目目录（V4-07）。
    /// </summary>
    public HashSet<string> CurrentContextUsers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// V4-07 的决定：使用项目 → 提供生成的 <c>CurrentUser</c> / <c>CurrentTenant</c> 的项目目录。
    /// </summary>
    public Dictionary<string, string> CurrentContextProviders { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 第一遍扫描到的显式单例注册的类型名（<c>AddSingleton&lt;T&gt;</c> / <c>TryAddSingleton&lt;TService, T&gt;</c> / <c>AddHostedService&lt;T&gt;</c> 的实现类型；V4-05 判断 <c>IUnitOfWorkManager</c> 的消费方是否单例）。
    /// </summary>
    public HashSet<string> ExplicitSingletons { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 第一遍扫描到的 <c>IUnitOfWorkManager</c> 消费类：类名 → (所在项目目录, 是否调用了 <c>Begin</c>)（V4-05）。
    /// </summary>
    public Dictionary<string, (string ProjectDirectory, bool Begins)> UnitOfWorkManagerConsumers { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 迁移范围内声明 <c>IUnitOfWorkManager</c> 接口的命名空间（应用已承接 4.x 工作单元管理器）；没有为 <see langword="null"/>。
    /// </summary>
    public string? UnitOfWorkManagerNamespace { get; set; }

    /// <summary>
    /// V4-05 的决定：改为注入 <c>ICurrentUnitOfWorkAccessor</c> 的单例消费类 → 生成访问器的命名空间。
    /// </summary>
    public Dictionary<string, string> UnitOfWorkAccessorConsumers { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// V4-09 从 4.x 资源文件收集的文案：项目目录 → 区域 → (平铺键 → 模板)。
    /// </summary>
    public Dictionary<string, Dictionary<string, SortedDictionary<string, string>>> LocalizationResources { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 运行结束时要删除的文件（已转换的 4.x 资源文件）；与生成文件同路径的不删。
    /// </summary>
    public HashSet<string> Deletions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 迁移根目录（第一遍扫描时由编排器设置）；生成文件的相对路径据此计算。
    /// </summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>
    /// 各项目目录的换行符（取项目里第一个扫描到的文件）。
    /// </summary>
    public Dictionary<string, string> ProjectNewLines { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 项目的换行符；没记录过用环境默认。
    /// </summary>
    public string ProjectNewLine(string projectDirectory) => ProjectNewLines.GetValueOrDefault(projectDirectory, Environment.NewLine);

    /// <summary>
    /// 相对根目录的报告路径（<c>/</c> 分隔）。
    /// </summary>
    public string RelativePath(string absolutePath) => System.IO.Path.GetRelativePath(Root, absolutePath).Replace('\\', '/');

    /// <summary>
    /// 记一次改动。
    /// </summary>
    public void Changed(string rule, MigrationFile file, int line) => Findings.Add(new MigrationFinding(rule, file.RelativePath, line, string.Empty, IsMarker: false));

    /// <summary>
    /// 记一个标记。
    /// </summary>
    public void Marked(string rule, MigrationFile file, int line, string message) => Findings.Add(new MigrationFinding(rule, file.RelativePath, line, message, IsMarker: true));

    /// <summary>
    /// 从项目文件读 <c>ProjectReference</c>（<c>Include</c> 相对项目文件所在目录）进引用图；多条规则都可以调用，重复无害。
    /// </summary>
    public void ScanProjectReferences(MigrationFile file, string text)
    {
        if (file.ProjectDirectory is null)
        {
            return;
        }

        if (!ProjectReferences.TryGetValue(file.ProjectDirectory, out var references))
        {
            references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ProjectReferences[file.ProjectDirectory] = references;
        }

        foreach (Match match in ProjectReference().Matches(text))
        {
            var include = match.Groups["path"].Value.Replace('\\', System.IO.Path.DirectorySeparatorChar).Replace('/', System.IO.Path.DirectorySeparatorChar);
            if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(System.IO.Path.Combine(file.ProjectDirectory, include))) is { } directory)
            {
                references.Add(directory);
            }
        }
    }

    /// <summary>
    /// 项目的传递引用闭包（不含自己）。
    /// </summary>
    public HashSet<string> ReferenceClosure(string project)
    {
        var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>([project]);
        while (queue.TryDequeue(out var current))
        {
            if (!ProjectReferences.TryGetValue(current, out var next))
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
    /// 给使用项目选提供者：自己与引用闭包里的使用项目为候选，取其中不再引用别的候选的那个（最底层）；并列取字典序最小，没有就是自己。
    /// </summary>
    public string LowestProvider(string user, IReadOnlySet<string> users)
    {
        var candidates = ReferenceClosure(user).Where(users.Contains).Append(user).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return candidates
            .Where(candidate => !ReferenceClosure(candidate).Any(reference => candidates.Contains(reference, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(static candidate => candidate, StringComparer.Ordinal)
            .FirstOrDefault() ?? user;
    }

    /// <summary>
    /// 单行的 <c>&lt;ProjectReference Include="…" /&gt;</c>。
    /// </summary>
    [GeneratedRegex("<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"")]
    private static partial Regex ProjectReference();
}
