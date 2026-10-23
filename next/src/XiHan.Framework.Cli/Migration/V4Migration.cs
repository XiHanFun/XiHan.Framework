// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一次 <c>xihan migrate v4</c> 的编排：枚举文件、第一遍扫描、按顺序施加规则、回写、写生成文件、改名、汇总报告。
/// </summary>
/// <param name="path">目录、<c>.csproj</c> 或 <c>.slnx</c>；文件取其所在目录。</param>
/// <param name="fixers">要执行的规则，按顺序。</param>
/// <param name="dryRun">只报告不写文件。</param>
internal sealed class V4Migration(string path, IReadOnlyList<V4Fixer> fixers, bool dryRun)
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
    /// 操作契约的源生成器包。
    /// </summary>
    private const string GeneratorsPackage = "XiHan.Framework.Kernel.Generators";

    /// <summary>
    /// 执行。
    /// </summary>
    public MigrationReport Run(CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
        var session = new MigrationSession { Root = root };
        var projectDirectories = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var newLines = session.ProjectNewLines;

        // 第一遍：读入并扫描（跨文件事实），文本留在内存里给第二遍用
        var sources = new List<(MigrationFile File, string Text, bool Bom)>();
        foreach (var file in Enumerate(root).OrderBy(static file => file, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var bytes = File.ReadAllBytes(file);
            var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var projectDirectory = ProjectDirectoryOf(Path.GetDirectoryName(file)!, root, projectDirectories);
            if (projectDirectory is not null)
            {
                newLines.TryAdd(projectDirectory, newLine);
            }

            var migrationFile = new MigrationFile(file, relative, projectDirectory, newLine);
            sources.Add((migrationFile, text, bom));
            foreach (var fixer in fixers)
            {
                if (fixer.Handles(migrationFile))
                {
                    fixer.Scan(migrationFile, text, session);
                }
            }
        }

        // 扫描完再做跨文件决定
        foreach (var fixer in fixers)
        {
            fixer.Prepare(session);
        }

        // 第二遍：按规则顺序改写
        foreach (var (file, text, bom) in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = text;
            foreach (var fixer in fixers)
            {
                if (fixer.Handles(file))
                {
                    current = fixer.Apply(file, current, session);
                }
            }

            if (!ReferenceEquals(current, text) && !string.Equals(current, text, StringComparison.Ordinal) && !dryRun)
            {
                File.WriteAllBytes(file.Path, bom ? [.. Encoding.UTF8.Preamble, .. _utf8.GetBytes(current)] : _utf8.GetBytes(current));
            }
        }

        // V4-03：每个项目一个注册文件；第一行留调用提醒
        foreach (var (projectDirectory, registrations) in session.Registrations.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var (project, className) = V4ServicesGenerator.Names(projectDirectory);
            var target = Path.Combine(projectDirectory, className + ".cs");
            var newLine = newLines.GetValueOrDefault(projectDirectory, Environment.NewLine);
            var generated = new MigrationFile(target, Path.GetRelativePath(root, target).Replace('\\', '/'), projectDirectory, newLine);
            var message = CliMessages.MigrateMarkerRegisterServices.L(("method", "Add" + className));
            session.Marked("V4-03", generated, 1, message);
            session.Changed("V4-03", generated, 0);
            session.GeneratedFiles[target] = V4ServicesGenerator.Generate(project, className, registrations, Markers.Line("V4-03", message), newLine);
        }

        // V4-06：空响应、序列化上下文，以及项目文件里的生成器引用
        V4OperationsGenerator.Complete(session, root);
        foreach (var (projectDirectory, project) in session.OperationProjects.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var csproj = Directory.EnumerateFiles(projectDirectory, "*.csproj").OrderBy(static name => name, StringComparer.Ordinal).First();
            var text = File.ReadAllText(csproj);
            if (text.Contains(GeneratorsPackage, StringComparison.Ordinal))
            {
                continue;
            }

            var file = new MigrationFile(csproj, Path.GetRelativePath(root, csproj).Replace('\\', '/'), projectDirectory, project.NewLine);
            session.Changed("V4-06", file, text.Count(static c => c == '\n') + 1);
            if (!dryRun)
            {
                File.WriteAllText(csproj, WithItemGroup(text, project.NewLine, $"<PackageReference Include=\"{GeneratorsPackage}\" Version=\"{FrameworkInfo.Version}\" PrivateAssets=\"all\" />"), _utf8);
            }
        }

        // V4-09：项目文件补嵌入资源项，第一行是装配目录的提醒
        foreach (var projectDirectory in session.LocalizationResources.Keys.OrderBy(static key => key, StringComparer.Ordinal))
        {
            var csproj = Directory.EnumerateFiles(projectDirectory, "*.csproj").OrderBy(static name => name, StringComparer.Ordinal).First();
            var text = File.ReadAllText(csproj);
            if (text.Contains("LogicalName=\"Localization/", StringComparison.Ordinal))
            {
                continue;
            }

            var newLine = session.ProjectNewLine(projectDirectory);
            var file = new MigrationFile(csproj, Path.GetRelativePath(root, csproj).Replace('\\', '/'), projectDirectory, newLine);
            var message = CliMessages.MigrateMarkerLocalizationCatalog.L(("domain", V4LocalizationGenerator.Domain(V4ServicesGenerator.Names(projectDirectory).Project)));
            var line = text.Count(static c => c == '\n') + 1;
            session.Marked("V4-09", file, line, message);
            session.Changed("V4-09", file, line);
            if (!dryRun)
            {
                File.WriteAllText(csproj, WithItemGroup(text, newLine, "<!-- " + Markers.Text("V4-09", message) + " -->", "<EmbeddedResource Include=\"Localization/*.json\" LogicalName=\"Localization/%(Filename)%(Extension)\" />"), _utf8);
            }
        }

        foreach (var (target, content) in session.GeneratedFiles.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, content, _utf8);
            }
        }

        // 已转换的 4.x 文件：生成文件同路径的不删（原文件就是目标文件时已被覆盖）
        foreach (var target in session.Deletions.Where(target => !session.GeneratedFiles.ContainsKey(target)).OrderBy(static target => target, StringComparer.Ordinal))
        {
            if (!dryRun && File.Exists(target))
            {
                File.Delete(target);
            }
        }

        // V4-01：模块文件随类名改名；目标已存在时不动
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
        return new MigrationReport("v4", root.Replace('\\', '/'), dryRun, rules, markers);
    }

    /// <summary>
    /// 在 <c>&lt;/Project&gt;</c> 前加一个 ItemGroup（每行一项），缩进沿用文件里第一个 ItemGroup / PropertyGroup。
    /// </summary>
    private static string WithItemGroup(string text, string newLine, params string[] items)
    {
        var close = text.LastIndexOf("</Project>", StringComparison.Ordinal);
        if (close < 0)
        {
            return text;
        }

        var indent = "  ";
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("<ItemGroup", StringComparison.Ordinal) || trimmed.StartsWith("<PropertyGroup", StringComparison.Ordinal))
            {
                indent = line[..(line.Length - trimmed.Length)].TrimEnd('\r');
                break;
            }
        }

        var builder = new StringBuilder();
        builder.Append(text, 0, close);
        if (!text[..close].EndsWith(newLine + newLine, StringComparison.Ordinal))
        {
            builder.Append(newLine);
        }

        builder.Append(indent).Append("<ItemGroup>").Append(newLine);
        foreach (var item in items)
        {
            builder.Append(indent).Append(indent).Append(item).Append(newLine);
        }

        builder.Append(indent).Append("</ItemGroup>").Append(newLine).Append(newLine);
        builder.Append(text, close, text.Length - close);
        return builder.ToString();
    }

    /// <summary>
    /// 递归枚举 <c>*.cs</c>、<c>*.csproj</c> 与 <c>Localization/*.json</c>，跳过 bin / obj / .git 与生成文件。
    /// </summary>
    private static IEnumerable<string> Enumerate(string directory)
    {
        var localization = string.Equals(Path.GetFileName(directory), "Localization", StringComparison.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || (localization && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                || (name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)))
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
    /// 最近的含 <c>.csproj</c> 的祖先目录（不越过根）；按目录缓存。
    /// </summary>
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
