#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PathResolver
// Guid:35906a83-4e75-42ad-a6bf-ed79114a5bfa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/23 05:38:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Core;

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
    /// <param name="virtualPath"></param>
    /// <returns></returns>
    public static string ResolveVirtualPath(string virtualPath)
    {
        ArgumentNullException.ThrowIfNull(virtualPath);

        var normalizedPath = virtualPath.Trim().Replace('\\', '/');
        if (normalizedPath.Length == 0)
        {
            return normalizedPath;
        }

        if (normalizedPath.StartsWith("~/", StringComparison.Ordinal))
        {
            return normalizedPath[1..];
        }

        if (normalizedPath.StartsWith('/', StringComparison.Ordinal))
        {
            return normalizedPath;
        }

        if (normalizedPath.StartsWith("embedded://", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveEmbeddedPath(normalizedPath);
        }

        if (MemoryPathPrefixes.Any(prefix => normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return ResolveMemoryPath(normalizedPath);
        }

        return RegexHelper.VirtualPathRegex().IsMatch(normalizedPath)
            ? normalizedPath
            : normalizedPath;
    }

    /// <summary>
    /// 解析嵌入式文件路径
    /// </summary>
    /// <param name="embeddedPath"></param>
    /// <returns></returns>
    public static string ResolveEmbeddedPath(string embeddedPath)
    {
        ArgumentNullException.ThrowIfNull(embeddedPath);

        var normalizedPath = embeddedPath.Trim().Replace('\\', '/');
        var match = RegexHelper.EmbeddedPathRegex().Match(normalizedPath);
        if (match is { Success: true })
        {
            var path = match.Groups["path"].Value.TrimStart('/');
            return "/" + path;
        }

        return normalizedPath;
    }

    /// <summary>
    /// 解析内存路径
    /// </summary>
    /// <param name="memoryPath"></param>
    /// <returns></returns>
    public static string ResolveMemoryPath(string memoryPath)
    {
        ArgumentNullException.ThrowIfNull(memoryPath);

        var normalizedPath = memoryPath.Trim().Replace('\\', '/');
        foreach (var prefix in MemoryPathPrefixes)
        {
            if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var path = normalizedPath[prefix.Length..].TrimStart('/');
                return "/" + path;
            }
        }

        return normalizedPath;
    }
}
