// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Http.Extensions;
using XiHan.Framework.Http.Extensions.DependencyInjection;
using XiHan.Framework.Serialization;

namespace XiHan.Framework.Http;

/// <summary>
/// 曦寒框架网络请求模块
/// </summary>
[DependsOn(
    typeof(XiHanSerializationModule)
    )]
public class XiHanHttpModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 使用扩展方法添加服务
        services.AddXiHanHttpModule(config);
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 使用构建器初始化应用
        StringHttpExtensions.Initialize(context.ServiceProvider);
    }
}
