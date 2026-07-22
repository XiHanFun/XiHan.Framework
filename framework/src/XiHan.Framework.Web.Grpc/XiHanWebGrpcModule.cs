// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Serialization;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.Core.Extensions;
using XiHan.Framework.Web.Grpc.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Grpc;

/// <summary>
/// 曦寒框架 Web gRPC 服务端模块
/// </summary>
[DependsOn(
    typeof(XiHanWebCoreModule),
    typeof(XiHanSerializationModule)
)]
public class XiHanWebGrpcModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanWebGrpc();
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
    }
}
