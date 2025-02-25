#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IVirtualFileResourceFactory
// Guid:5f4e3d2c-1b0a-9f8e-7d6c-5b4a3f2e1d0b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/27 12:30:18
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Localization.Resources;

namespace XiHan.Framework.Localization.VirtualFileSystem;

/// <summary>
/// 虚拟文件资源工厂接口
/// </summary>
public interface IVirtualFileResourceFactory
{
    /// <summary>
    /// 创建虚拟文件资源
    /// </summary>
    /// <param name="virtualPath">虚拟路径</param>
    /// <returns>本地化资源</returns>
    ILocalizationResource Create(string virtualPath);
}
