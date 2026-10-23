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
    /// 记一次改动。
    /// </summary>
    public void Changed(string rule, MigrationFile file, int line) => Findings.Add(new MigrationFinding(rule, file.RelativePath, line, string.Empty, IsMarker: false));

    /// <summary>
    /// 记一个标记。
    /// </summary>
    public void Marked(string rule, MigrationFile file, int line, string message) => Findings.Add(new MigrationFinding(rule, file.RelativePath, line, message, IsMarker: true));
}
