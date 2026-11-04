// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Cli.Integration;

/// <summary>
/// 识别当前项目的根目录与引用的框架版本
/// </summary>
/// <remarks>
/// 从起点向上定位项目根，再向下扫描项目文件与 <c>.props</c> 里的 <c>XiHan.Framework.*</c> 包引用（含中央包管理的 <c>PackageVersion</c>）；扫描有文件数上限，跳过构建产物与依赖目录
/// </remarks>
internal static partial class ProjectDetector
{
    /// <summary>
    /// 最多扫描的构建文件数
    /// </summary>
    private const int MaxFiles = 2000;

    /// <summary>
    /// 不进入的目录
    /// </summary>
    private static readonly HashSet<string> _skippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules", "dist", "artifacts", "TestResults",
    };

    /// <summary>
    /// 识别
    /// </summary>
    public static ProjectInfo Detect(string start)
    {
        ArgumentNullException.ThrowIfNull(start);
        var root = Directory.Exists(start) ? FindRoot(start) : null;
        if (root is null)
        {
            return new ProjectInfo(null, null);
        }

        var versions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in EnumerateBuildFiles(root).Take(MaxFiles))
        {
            foreach (Match match in PackageReference().Matches(File.ReadAllText(file)))
            {
                var version = match.Groups["version"].Value;
                versions[version] = versions.GetValueOrDefault(version) + 1;
            }
        }

        // 同一项目混用多个版本时取引用次数最多的；次数相同取字典序靠后的（通常是较新的）
        var detected = versions.Count == 0 ? null : versions.OrderByDescending(static p => p.Value).ThenByDescending(static p => p.Key, StringComparer.Ordinal).First().Key;
        return new ProjectInfo(root, detected);
    }

    /// <summary>
    /// 向上找项目根：先找 git 仓库根，再找含解决方案或项目文件的目录
    /// </summary>
    private static string? FindRoot(string start)
        => FindUp(start, static d => Directory.Exists(Path.Combine(d.FullName, ".git")) || File.Exists(Path.Combine(d.FullName, ".git")))
            ?? FindUp(start, static d => d.EnumerateFiles("*.sln*").Any() || d.EnumerateFiles("*.csproj").Any());

    /// <summary>
    /// 从起点向上找第一个满足条件的目录
    /// </summary>
    private static string? FindUp(string start, Func<DirectoryInfo, bool> match)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory is not null; directory = directory.Parent)
        {
            if (match(directory))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// 深度优先列出项目文件与 <c>.props</c>
    /// </summary>
    private static IEnumerable<string> EnumerateBuildFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (!_skippedDirectories.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".props", StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }

    /// <summary>
    /// <c>PackageReference</c> 或 <c>PackageVersion</c> 里的框架包版本
    /// </summary>
    [GeneratedRegex("""<Package(?:Reference|Version)\s+Include="XiHan\.Framework\.[\w.]+"\s+Version="(?<version>[^"$]+)"\s*/?>""")]
    private static partial Regex PackageReference();
}
