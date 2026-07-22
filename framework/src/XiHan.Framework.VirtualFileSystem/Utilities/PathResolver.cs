// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.VirtualFileSystem.Utilities;

/// <summary>
/// 路径解析服务
/// </summary>
public static class PathResolver
{
    private static readonly string[] MemoryPathPrefixes =
    [
        "memory://",
        "mem://"
    ];

    /// <summary>
    /// 解析虚拟路径
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>标准化后的虚拟路径</returns>
    public static string ResolveVirtualPath(string virtualPath)
    {
        ArgumentNullException.ThrowIfNull(virtualPath);

        var normalizedPath = virtualPath.Trim().Replace('\\', '/');
        if (normalizedPath.Length == 0)
        {
            return "/";
        }

        if (normalizedPath.StartsWith("~/", StringComparison.Ordinal))
        {
            return NormalizeVirtualPath(normalizedPath[1..]);
        }

        if (normalizedPath.StartsWith("embedded://", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveEmbeddedPath(normalizedPath);
        }

        if (MemoryPathPrefixes.Any(prefix => normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return ResolveMemoryPath(normalizedPath);
        }

        return NormalizeVirtualPath(normalizedPath);
    }

    /// <summary>
    /// 标准化虚拟路径
    /// </summary>
    /// <param name="path">输入路径</param>
    /// <param name="keepTrailingSlash">是否保留尾部斜杠</param>
    /// <returns>标准化后的路径</returns>
    public static string NormalizeVirtualPath(string path, bool keepTrailingSlash = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        if (!keepTrailingSlash)
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized.Length == 0 ? "/" : normalized;
    }

    /// <summary>
    /// 合并虚拟路径
    /// </summary>
    /// <param name="left">左路径</param>
    /// <param name="right">右路径</param>
    /// <returns>合并后的虚拟路径</returns>
    public static string CombineVirtualPath(string left, string right)
    {
        var normalizedLeft = NormalizeVirtualPath(left).TrimEnd('/');
        var normalizedRight = (right ?? string.Empty).Replace('\\', '/').TrimStart('/');

        if (string.IsNullOrWhiteSpace(normalizedRight))
        {
            return normalizedLeft.Length == 0 ? "/" : normalizedLeft;
        }

        if (normalizedLeft == "/")
        {
            return "/" + normalizedRight;
        }

        return $"{normalizedLeft}/{normalizedRight}";
    }

    /// <summary>
    /// 判断路径是否在指定根路径下
    /// </summary>
    /// <param name="path">待判断路径</param>
    /// <param name="rootPath">根路径</param>
    /// <returns>是否位于根路径下</returns>
    public static bool IsPathUnder(string path, string rootPath)
    {
        var normalizedPath = NormalizeVirtualPath(path);
        var normalizedRoot = NormalizeVirtualPath(rootPath);

        if (normalizedRoot == "/")
        {
            return true;
        }

        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith($"{normalizedRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解析嵌入式文件路径
    /// </summary>
    /// <param name="embeddedPath">嵌入式路径</param>
    /// <returns>标准化后的虚拟路径</returns>
    public static string ResolveEmbeddedPath(string embeddedPath)
    {
        ArgumentNullException.ThrowIfNull(embeddedPath);

        var normalizedPath = embeddedPath.Trim().Replace('\\', '/');
        var prefixIndex = normalizedPath.IndexOf("embedded://", StringComparison.OrdinalIgnoreCase);
        var relative = prefixIndex >= 0
            ? normalizedPath[(prefixIndex + "embedded://".Length)..]
            : normalizedPath;

        var firstSlash = relative.IndexOf('/', StringComparison.Ordinal);
        if (firstSlash < 0)
        {
            return "/";
        }

        return NormalizeVirtualPath(relative[firstSlash..]);
    }

    /// <summary>
    /// 解析内存路径
    /// </summary>
    /// <param name="memoryPath">内存路径</param>
    /// <returns>标准化后的虚拟路径</returns>
    public static string ResolveMemoryPath(string memoryPath)
    {
        ArgumentNullException.ThrowIfNull(memoryPath);

        var normalizedPath = memoryPath.Trim().Replace('\\', '/');
        foreach (var prefix in MemoryPathPrefixes)
        {
            if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var path = normalizedPath[prefix.Length..].TrimStart('/');
                return NormalizeVirtualPath(path);
            }
        }

        return NormalizeVirtualPath(normalizedPath);
    }
}
