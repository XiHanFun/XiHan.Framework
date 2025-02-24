#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PathResolver
// Guid:35906a83-4e75-42ad-a6bf-ed79114a5bfa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/23 5:38:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.VirtualFileSystem.Utilities;

/// <summary>
/// 路径解析服务
/// </summary>
public static partial class PathResolver
{
    /// <summary>
    /// 解析虚拟路径
    /// </summary>
    /// <param name="virtualPath"></param>
    /// <returns></returns>
    public static string ResolveVirtualPath(string virtualPath)
    {
        return RegexHelper.VirtualPathRegex().Match(virtualPath) is { Success: true } match
            ? match.Groups["path"].Value
            : virtualPath;
    }

    /// <summary>
    /// 解析嵌入式文件路径
    /// </summary>
    /// <param name="embeddedPath"></param>
    /// <returns></returns>
    public static string ResolveEmbeddedPath(string embeddedPath)
    {
        return RegexHelper.EmbeddedPathRegex().Match(embeddedPath) is { Success: true } match
            ? match.Groups["path"].Value
            : embeddedPath;
    }

    /// <summary>
    /// 解析内存路径
    /// </summary>
    /// <param name="memoryPath"></param>
    /// <returns></returns>
    public static string ResolveMemoryPath(string memoryPath)
    {
        return RegexHelper.MemoryPathRegex().Match(memoryPath) is { Success: true } match
            ? match.Groups["path"].Value
            : memoryPath;
    }
}
