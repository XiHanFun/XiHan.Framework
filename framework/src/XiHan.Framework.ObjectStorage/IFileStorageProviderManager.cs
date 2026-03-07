#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IFileStorageProviderManager
// Guid:4e639f20-47bc-47cc-b7eb-8bde8b5e8f52
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 01:47:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectStorage;

/// <summary>
/// 文件存储提供程序管理器
/// </summary>
public interface IFileStorageProviderManager
{
    /// <summary>
    /// 获取提供程序
    /// </summary>
    /// <param name="providerName">提供程序名称，空则使用默认提供程序</param>
    /// <returns>文件存储提供程序</returns>
    IFileStorageProvider GetProvider(string? providerName = null);

    /// <summary>
    /// 尝试获取提供程序
    /// </summary>
    /// <param name="providerName">提供程序名称，空则使用默认提供程序</param>
    /// <param name="provider">文件存储提供程序</param>
    /// <returns>获取成功返回 true</returns>
    bool TryGetProvider(string? providerName, out IFileStorageProvider? provider);

    /// <summary>
    /// 获取已注册的提供程序名称
    /// </summary>
    /// <returns>提供程序名称列表</returns>
    IReadOnlyList<string> GetRegisteredProviderNames();
}
