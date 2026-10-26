// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一次迁移运行的共享状态：结果，规则自己的跨文件事实，以及规则登记、由编排器在改写回写之后执行的生成、删除与改名。
/// </summary>
internal sealed class MigrationSession
{
    /// <summary>
    /// 规则自己的跨文件事实，按状态类型各一份。
    /// </summary>
    private readonly Dictionary<Type, object> _states = [];

    /// <summary>
    /// 全部结果，按产生顺序。
    /// </summary>
    public List<MigrationFinding> Findings { get; } = [];

    /// <summary>
    /// 要写出的生成文件：绝对路径 → 文本；经 <see cref="Generate"/> 登记。
    /// </summary>
    public Dictionary<string, string> GeneratedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 运行结束时要删除的文件（已转换的旧文件）；与生成文件同路径的不删。
    /// </summary>
    public HashSet<string> Deletions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 要改名的文件：原绝对路径 → 新绝对路径；目标已存在时不改。
    /// </summary>
    public Dictionary<string, string> Renames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 迁移根目录（由编排器设置）；报告里的相对路径据此计算。
    /// </summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>
    /// 各项目目录的换行符（取项目里第一个读入的文件）。
    /// </summary>
    public Dictionary<string, string> ProjectNewLines { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 项目的换行符；没记录过用环境默认。生成文件按它写换行。
    /// </summary>
    public string ProjectNewLine(string projectDirectory) => ProjectNewLines.GetValueOrDefault(projectDirectory, Environment.NewLine);

    /// <summary>
    /// 相对根目录的报告路径（<c>/</c> 分隔）。
    /// </summary>
    public string RelativePath(string absolutePath) => Path.GetRelativePath(Root, absolutePath).Replace('\\', '/');

    /// <summary>
    /// 规则的跨文件事实：同一状态类型每次运行一份，第一次取时创建。
    /// </summary>
    /// <remarks>规则集是静态登记的，规则实例跨运行复用，所以事实不能存在规则实例上，只能存在会话里。</remarks>
    public TState StateOf<TState>()
        where TState : class, new()
    {
        if (!_states.TryGetValue(typeof(TState), out var state))
        {
            state = new TState();
            _states[typeof(TState)] = state;
        }

        return (TState)state;
    }

    /// <summary>
    /// 记一次改动。
    /// </summary>
    public void Changed(string rule, MigrationFile file, int line) => Findings.Add(new MigrationFinding(rule, file.RelativePath, line, string.Empty, IsMarker: false));

    /// <summary>
    /// 记一个标记。
    /// </summary>
    public void Marked(string rule, MigrationFile file, int line, string message) => Findings.Add(new MigrationFinding(rule, file.RelativePath, line, message, IsMarker: true));

    /// <summary>
    /// 登记一个生成文件；磁盘上已有完全相同的内容时不登记、不算改动（再跑一遍零改动），否则记在该规则下一次改动（行号 0）。
    /// </summary>
    /// <returns>是否登记（内容有变化）。</returns>
    public bool Generate(string rule, string absolutePath, string content)
    {
        // 同一次运行里后登记的覆盖先登记的；与磁盘比较只看最终要写出的内容
        if (File.Exists(absolutePath) && string.Equals(File.ReadAllText(absolutePath), content, StringComparison.Ordinal))
        {
            return false;
        }

        GeneratedFiles[absolutePath] = content;
        Findings.Add(new MigrationFinding(rule, RelativePath(absolutePath), 0, string.Empty, IsMarker: false));
        return true;
    }
}
