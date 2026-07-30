// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Observability.Extensions.DependencyInjection;

namespace XiHan.Framework.Observability;

/// <summary>
/// 曦寒可观测性模块
/// </summary>
public class XiHanObservabilityModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 使用扩展方法添加可观测性服务
        services.AddXiHanObservability(config);
    }
}
