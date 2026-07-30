// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.Modularity;

/// <summary>
/// 服务配置前接口
/// </summary>
public interface IPreConfigureServices
{
    /// <summary>
    /// 服务配置前，异步
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task PreConfigureServicesAsync(ServiceConfigurationContext context);

    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context"></param>
    void PreConfigureServices(ServiceConfigurationContext context);
}
