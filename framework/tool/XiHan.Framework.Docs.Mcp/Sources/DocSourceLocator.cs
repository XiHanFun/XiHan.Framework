// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Docs.Mcp.Sources;

/// <summary>
/// 找不到仓库根时抛出
/// </summary>
/// <remarks>
/// 刻意让进程启动失败，而不是带着空索引进入服务状态——
/// 一个「连得上但永远搜不到东西」的服务端比一个起不来的更难排查。
/// </remarks>
public sealed class DocsRootNotFoundException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">错误信息</param>
    public DocsRootNotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// 定位仓库根并枚举四类文档来源
/// </summary>
/// <param name="repositoryRoot">仓库根的绝对路径</param>
public sealed class DocSourceLocator(string repositoryRoot)
{
    /// <summary>
    /// 仓库根的绝对路径
    /// </summary>
    public string RepositoryRoot { get; } = Path.GetFullPath(repositoryRoot);

    /// <summary>
    /// 解析仓库根：环境变量优先，否则从指定目录逐层向上查找
    /// </summary>
    /// <param name="startDirectory">向上查找的起点目录</param>
    /// <param name="environmentOverride">环境变量 XIHAN_DOCS_ROOT 的值，可为空</param>
    /// <returns>仓库根的绝对路径</returns>
    /// <exception cref="DocsRootNotFoundException">无法定位仓库根时抛出</exception>
    public static string ResolveRepositoryRoot(string startDirectory, string? environmentOverride)
    {
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            if (IsRepositoryRoot(environmentOverride))
            {
                return Path.GetFullPath(environmentOverride);
            }

            throw new DocsRootNotFoundException(
                $"环境变量 XIHAN_DOCS_ROOT 指向 '{environmentOverride}'，但该目录下缺少 docs 或 framework 子目录。");
        }

        var attempted = new List<string>();
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (current is not null)
        {
            attempted.Add(current.FullName);
            if (IsRepositoryRoot(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DocsRootNotFoundException(
            $"""
             无法定位曦寒框架仓库根：从 '{startDirectory}' 逐层向上均未找到同时包含 docs 与 framework 子目录的路径。
             已尝试：{string.Join(" -> ", attempted)}
             请设置环境变量 XIHAN_DOCS_ROOT 指向仓库根目录后重试。
             """);
    }

    /// <summary>
    /// 枚举全部待索引文档
    /// </summary>
    /// <returns>按相对路径排序的文档文件列表</returns>
    public IReadOnlyList<DocFile> Enumerate()
    {
        var files = new List<DocFile>();

        CollectDirectory(files, Path.Combine(RepositoryRoot, "docs", "guide"), DocSourceKind.Guide);
        CollectDirectory(files, Path.Combine(RepositoryRoot, "docs", "packages"), DocSourceKind.Package);
        CollectDirectory(files, Path.Combine(RepositoryRoot, "docs"), DocSourceKind.Root);
        CollectPackageReadmes(files);

        return [.. files.OrderBy(f => f.RelativePath, StringComparer.Ordinal)];
    }

    /// <summary>
    /// 把相对路径解析为绝对路径，并校验其未逃逸仓库根
    /// </summary>
    /// <param name="relativePath">相对仓库根的路径</param>
    /// <param name="absolutePath">解析出的绝对路径</param>
    /// <returns>路径合法且位于仓库根内时为 true</returns>
    public bool TryResolveDocumentPath(string relativePath, out string absolutePath)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(RepositoryRoot, relativePath));
        var prefix = RepositoryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        absolutePath = combined;
        return true;
    }

    /// <summary>
    /// 判断目录是否同时包含 docs 与 framework 子目录
    /// </summary>
    private static bool IsRepositoryRoot(string directory)
    {
        return Directory.Exists(Path.Combine(directory, "docs"))
            && Directory.Exists(Path.Combine(directory, "framework"));
    }

    /// <summary>
    /// 收集一个目录下的 Markdown 文件，仅顶层不递归
    /// </summary>
    private void CollectDirectory(List<DocFile> files, string directory, DocSourceKind source)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
        {
            files.Add(CreateDocFile(path, source));
        }
    }

    /// <summary>
    /// 收集各包目录下的 README，仅取包目录的直接子文件，天然排除 obj 与 bin
    /// </summary>
    private void CollectPackageReadmes(List<DocFile> files)
    {
        var srcRoot = Path.Combine(RepositoryRoot, "framework", "src");
        if (!Directory.Exists(srcRoot))
        {
            return;
        }

        foreach (var packageDirectory in Directory.EnumerateDirectories(srcRoot))
        {
            var readme = Path.Combine(packageDirectory, "README.md");
            if (File.Exists(readme))
            {
                files.Add(CreateDocFile(readme, DocSourceKind.PackageReadme));
            }
        }
    }

    /// <summary>
    /// 由绝对路径构造文档文件描述
    /// </summary>
    private DocFile CreateDocFile(string absolutePath, DocSourceKind source)
    {
        var relative = Path.GetRelativePath(RepositoryRoot, absolutePath).Replace('\\', '/');
        return new DocFile(absolutePath, relative, source, File.GetLastWriteTimeUtc(absolutePath));
    }
}
