#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileLocalizationResource
// Guid:3d2c1b0a-9f8e-7d6c-5b4a-3f2e1d0b5c4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:32:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.VirtualFileSystem;

namespace XiHan.Framework.Localization.Resources;

/// <summary>
/// 虚拟文件本地化资源
/// </summary>
public class VirtualFileLocalizationResource : BaseLocalizationResource
{
    private readonly IVirtualFileSystem _virtualFileSystem;
    private readonly string _virtualPath;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="resourceName"></param>
    /// <param name="virtualPath"></param>
    /// <param name="virtualFileSystem"></param>
    /// <param name="defaultCulture"></param>
    /// <param name="priority"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public VirtualFileLocalizationResource(
        string resourceName,
        string virtualPath,
        IVirtualFileSystem virtualFileSystem,
        string defaultCulture = "en",
        int priority = 0)
        : base(resourceName, defaultCulture, virtualPath, priority)
    {
        _virtualFileSystem = virtualFileSystem ?? throw new ArgumentNullException(nameof(virtualFileSystem));
        _virtualPath = virtualPath ?? throw new ArgumentNullException(nameof(virtualPath));
    }

    /// <summary>
    /// 检查是否包含指定文件
    /// </summary>
    /// <param name="cultureName">文化名称</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>是否存在</returns>
    public bool Contains(string cultureName, string extension = ".json")
    {
        var fullPath = GetFilePath(cultureName, extension);
        var fileInfo = _virtualFileSystem.GetFile(fullPath);
        return fileInfo.Exists;
    }

    /// <summary>
    /// 获取本地化文件流
    /// </summary>
    /// <param name="cultureName">文化名称</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>文件流</returns>
    public Stream? GetStream(string cultureName, string extension = ".json")
    {
        var fullPath = GetFilePath(cultureName, extension);
        var fileInfo = _virtualFileSystem.GetFile(fullPath);

        return !fileInfo.Exists ? null : fileInfo.CreateReadStream();
    }

    /// <summary>
    /// 获取完整文件路径
    /// </summary>
    /// <param name="cultureName">文化名称</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>文件路径</returns>
    private string GetFilePath(string cultureName, string extension)
    {
        return Path.Combine(_virtualPath, $"{ResourceName}.{cultureName}{extension}");
    }
}
