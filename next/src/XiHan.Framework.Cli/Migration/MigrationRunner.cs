// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一次 <c>xihan migrate</c> 的编排（<c>spec/migration/README.md</c>）。
/// </summary>
/// <remarks>
/// 枚举文件、第一遍扫描、跨文件决定、按顺序施加规则并回写，再写生成文件、删除与改名，最后汇总报告。
/// </remarks>
/// <param name="path">目录、<c>.csproj</c> 或 <c>.slnx</c>；文件取其所在目录。</param>
/// <param name="version">来源版本，原样写进报告。</param>
/// <param name="fixers">要执行的规则，按顺序。</param>
/// <param name="dryRun">只报告不写文件。</param>
internal sealed class MigrationRunner(string path, string version, IReadOnlyList<MigrationFixer> fixers, bool dryRun)
{
    /// <summary>
    /// 跳过的目录名。
    /// </summary>
    private static readonly string[] _skippedDirectories = ["bin", "obj", ".git", ".vs", "node_modules"];

    /// <summary>
    /// 不带 BOM 的 UTF-8。
    /// </summary>
    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// 执行。
    /// </summary>
    public MigrationReport Run(CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
        var session = new MigrationSession { Root = root };
        var projectDirectories = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // 第一遍：只读入有规则处理的文件并扫描（跨文件事实），文本留在内存里给第二遍用
        var sources = new List<(MigrationFile File, string Text, bool Bom, List<MigrationFixer> Handlers)>();
        foreach (var file in Enumerate(root).OrderBy(static file => file, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var projectDirectory = ProjectDirectoryOf(Path.GetDirectoryName(file)!, root, projectDirectories);
            // Handles 只看路径：换行符要读了文件才知道，先用占位值判定，没有规则处理的文件不读
            var probe = new MigrationFile(file, relative, projectDirectory, Environment.NewLine);
            var handlers = fixers.Where(fixer => fixer.Handles(probe)).ToList();
            if (handlers.Count == 0)
            {
                continue;
            }

            var bytes = File.ReadAllBytes(file);
            var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            if (projectDirectory is not null)
            {
                session.ProjectNewLines.TryAdd(projectDirectory, newLine);
            }

            var migrationFile = probe with { NewLine = newLine };
            sources.Add((migrationFile, text, bom, handlers));
            foreach (var handler in handlers)
            {
                handler.Scan(migrationFile, text, session);
            }
        }

        // 扫描完再做跨文件决定
        foreach (var fixer in fixers)
        {
            fixer.Prepare(session);
        }

        // 第二遍：按规则顺序改写；后一条规则看到的是前一条改过的文本
        foreach (var (file, text, bom, handlers) in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = text;
            foreach (var handler in handlers)
            {
                current = handler.Apply(file, current, session);
            }

            if (!dryRun && !ReferenceEquals(current, text) && !string.Equals(current, text, StringComparison.Ordinal))
            {
                File.WriteAllBytes(file.Path, bom ? [.. Encoding.UTF8.Preamble, .. _utf8.GetBytes(current)] : _utf8.GetBytes(current));
            }
        }

        // 生成、删除与改名都在改写回写之后：规则在第二遍里登记，试运行时一律不执行
        foreach (var (target, content) in session.GeneratedFiles.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, content, _utf8);
            }
        }

        // 与生成文件同路径的不删：原文件就是目标文件时已被覆盖
        foreach (var target in session.Deletions.Where(target => !session.GeneratedFiles.ContainsKey(target)).OrderBy(static target => target, StringComparer.Ordinal))
        {
            if (!dryRun && File.Exists(target))
            {
                File.Delete(target);
            }
        }

        // 目标已存在时不动，避免覆盖人手写的文件
        foreach (var (from, to) in session.Renames.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!dryRun && File.Exists(from) && !File.Exists(to))
            {
                File.Move(from, to);
            }
        }

        var rules = fixers.Select(static fixer => fixer.Id).Distinct(StringComparer.Ordinal)
            .Select(id => new MigrationRuleReport(
                id,
                session.Findings.Count(finding => finding.Rule == id && !finding.IsMarker),
                session.Findings.Count(finding => finding.Rule == id && finding.IsMarker),
                session.Findings.Where(finding => finding.Rule == id).Select(static finding => finding.File).Distinct(StringComparer.Ordinal).OrderBy(static file => file, StringComparer.Ordinal).ToList()))
            .ToList();
        var markers = session.Findings.Where(static finding => finding.IsMarker).Select(static finding => new MigrationMarkerReport(finding.Rule, finding.File, finding.Line, finding.Message)).ToList();
        return new MigrationReport(version, root.Replace('\\', '/'), dryRun, rules, markers);
    }

    /// <summary>
    /// 递归枚举文件。
    /// </summary>
    /// <remarks>
    /// 跳过 bin / obj / .git / .vs / node_modules 与生成的 <c>*.g.cs</c>、<c>*.Designer.cs</c>。
    /// </remarks>
    private static IEnumerable<string> Enumerate(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(file);
            if (!name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (_skippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var file in Enumerate(child))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// 最近的含 <c>.csproj</c> 的祖先目录（不越过根）。
    /// </summary>
    /// <remarks>
    /// 按目录缓存。
    /// </remarks>
    private static string? ProjectDirectoryOf(string directory, string root, Dictionary<string, string?> cache)
    {
        if (cache.TryGetValue(directory, out var cached))
        {
            return cached;
        }

        string? result = null;
        if (Directory.EnumerateFiles(directory, "*.csproj").Any())
        {
            result = directory;
        }
        else if (!string.Equals(Path.GetFullPath(directory), root, StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(directory) is { } parent && parent.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            result = ProjectDirectoryOf(parent, root, cache);
        }

        cache[directory] = result;
        return result;
    }
}
