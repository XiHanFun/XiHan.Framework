// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.DistributedIds.Extensions.DependencyInjection;

namespace XiHan.Framework.DistributedIds;

/// <summary>
/// 曦寒框架分布式唯一标识生成模块
/// </summary>
public class XiHanDistributedIdsModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 注册分布式唯一标识生成器服务
        services.AddXiHanDistributedIds(config);
    }
}
