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
    /// <param name="virtualPath">输入的虚拟路径</param>
    /// <returns></returns>
    public static string ResolveVirtualPath(string virtualPath)
    {
        var match = RegexHelper.VirtualPathRegex().Match(virtualPath);
        return match.Success ? match.Groups["path"].Value : virtualPath;
    }

    /// <summary>
    /// 解析虚拟路径
    /// </summary>
    /// <param name="embeddedPath">输入的虚拟路径</param>
    /// <returns></returns>
    public static string ResolveEmbeddedPath(string embeddedPath)
    {
        var match = RegexHelper.EmbeddedPathRegex().Match(embeddedPath);
        return match.Success ? match.Groups["path"].Value : embeddedPath;
    }

    /// <summary>
    /// 内存路径解析
    /// </summary>
    /// <param name="memoryPath"></param>
    /// <returns></returns>
    public static string ResolveMemoryPath(string memoryPath)
    {
        var math = RegexHelper.MemoryPathRegex().Match(memoryPath);
        return math.Success ? math.Groups["path"].Value : memoryPath;
    }
}
