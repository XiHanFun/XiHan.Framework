// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
