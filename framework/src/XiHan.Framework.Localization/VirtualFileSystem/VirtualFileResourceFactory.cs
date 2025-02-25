#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:VirtualFileResourceFactory
// Guid:7d6c5b4a-3f2e-1d0b-5c4a-9e8d7f6a5b4c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:34:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Localization.Resources;
using XiHan.Framework.VirtualFileSystem;

namespace XiHan.Framework.Localization.VirtualFileSystem;

/// <summary>
/// 虚拟文件资源工厂
/// </summary>
public class VirtualFileResourceFactory : IVirtualFileResourceFactory, ISingletonDependency
{
    private readonly IVirtualFileSystem _virtualFileSystem;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="virtualFileSystem"></param>
    public VirtualFileResourceFactory(IVirtualFileSystem virtualFileSystem)
    {
        _virtualFileSystem = virtualFileSystem;
    }

    /// <summary>
    /// 创建资源
    /// </summary>
    /// <param name="virtualPath"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ILocalizationResource Create(string virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath))
        {
            throw new ArgumentException("Virtual path cannot be null or empty", nameof(virtualPath));
        }

        // 从虚拟路径获取资源名称
        var resourceName = Path.GetFileNameWithoutExtension(virtualPath);
        if (string.IsNullOrEmpty(resourceName))
        {
            resourceName = virtualPath.Replace("/", ".").TrimStart('.');
        }

        return new VirtualFileLocalizationResource(resourceName, virtualPath, _virtualFileSystem);
    }
}
