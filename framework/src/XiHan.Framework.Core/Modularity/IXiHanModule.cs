#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IXiHanModule
// Guid:b9659dcc-4149-44c5-8437-2e4dcad4c694
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/26 7:59:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 曦寒模块化服务配置接口
/// </summary>
public interface IXiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    void ConfigureServices(ServiceConfigurationContext context);

    /// <summary>
    /// 服务配置，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task ConfigureServicesAsync(ServiceConfigurationContext context);
}
