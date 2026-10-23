// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一次迁移运行的共享状态：结果、跨文件累积的注册项、要改名的文件。
/// </summary>
internal sealed class MigrationSession
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
    /// 用到 <c>ICurrentUser</c> / <c>ICurrentTenant</c> 的项目目录（V4-07）。
    /// </summary>
    public HashSet<string> CurrentContextUsers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// V4-07 的决定：使用项目 → 提供生成的 <c>CurrentUser</c> / <c>CurrentTenant</c> 的项目目录。
    /// </summary>
    public Dictionary<string, string> CurrentContextProviders { get; } = new(StringComparer.OrdinalIgnoreCase);

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
}
