// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Cli.Integration;

/// <summary>
/// 当前项目的位置与引用的框架版本
/// </summary>
/// <param name="Root">项目根目录：所在 git 仓库的根，不在仓库里时为向上第一个含解决方案或项目文件的目录</param>
/// <param name="FrameworkVersion">项目里 XiHan.Framework.* 包引用最常见的版本，没有引用时为 null</param>
internal sealed record ProjectInfo(string? Root, string? FrameworkVersion);

/// <summary>
/// 识别当前项目引用的 XiHan.Framework 版本
/// </summary>
internal static partial class ProjectDetector
{
    private const int MaxFiles = 2000;

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules", "dist", "artifacts", "TestResults",
    };

    /// <summary>
    /// 从 start 向上定位项目根，再向下扫描项目文件与 props 里的包引用
    /// </summary>
    public static ProjectInfo Detect(string start)
    {
        var root = FindRoot(start);
        if (root is null)
        {
            return new ProjectInfo(null, null);
        }

        var versions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in EnumerateBuildFiles(root).Take(MaxFiles))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (Match match in PackageReferenceRegex().Matches(text))
            {
                var version = match.Groups["version"].Value;
                versions[version] = versions.GetValueOrDefault(version) + 1;
            }
        }

        var detected = versions.Count == 0 ? null : versions.OrderByDescending(pair => pair.Value).First().Key;
        return new ProjectInfo(root, detected);
    }

    /// <summary>
    /// 项目引用的版本与工具数据版本不一致时的提示；一致或未引用时返回 null
    /// </summary>
    public static string? VersionNotice(ProjectInfo project, string dataVersion)
    {
        if (project.FrameworkVersion is null || project.FrameworkVersion == dataVersion)
        {
            return null;
        }

        return $"当前项目引用的是 XiHan.Framework {project.FrameworkVersion}，这份工具的数据对应 {dataVersion}。"
            + $"要按项目版本查询，改用 dnx XiHan.Framework.Cli@{project.FrameworkVersion} --yes -- <命令>。";
    }

    private static string? FindRoot(string start)
    {
        return FindUp(start, directory => Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            ?? FindUp(start, directory => directory.EnumerateFiles("*.sln*").Any() || directory.EnumerateFiles("*.csproj").Any());
    }

    private static string? FindUp(string start, Func<DirectoryInfo, bool> match)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (match(directory))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateBuildFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(directory).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (Directory.Exists(entry))
                {
                    if (!SkippedDirectories.Contains(Path.GetFileName(entry)))
                    {
                        pending.Push(entry);
                    }
                }
                else if (entry.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || entry.EndsWith(".props", StringComparison.OrdinalIgnoreCase))
                {
                    yield return entry;
                }
            }
        }
    }

    [GeneratedRegex("""<Package(?:Reference|Version)\s+Include="XiHan\.Framework\.[\w.]+"\s+Version="(?<version>[^"$]+)"\s*/?>""")]
    private static partial Regex PackageReferenceRegex();
}
