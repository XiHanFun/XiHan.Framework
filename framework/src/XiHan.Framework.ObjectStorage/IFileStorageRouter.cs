#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IFileStorageRouter
// Guid:74b99711-8fc4-44b8-a9dc-b5bf5eb6a4d2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 01:48:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectStorage;

/// <summary>
/// 文件存储路由器
/// </summary>
public interface IFileStorageRouter
{
    /// <summary>
    /// 解析提供程序名称
    /// </summary>
    /// <param name="routeKey">业务路由键</param>
    /// <param name="providerName">指定的提供程序名称（优先级最高）</param>
    /// <returns>解析后的提供程序名称</returns>
    string ResolveProviderName(string? routeKey = null, string? providerName = null);

    /// <summary>
    /// 路由并获取提供程序
    /// </summary>
    /// <param name="routeKey">业务路由键</param>
    /// <param name="providerName">指定的提供程序名称（优先级最高）</param>
    /// <returns>文件存储提供程序</returns>
    IFileStorageProvider Route(string? routeKey = null, string? providerName = null);
}
