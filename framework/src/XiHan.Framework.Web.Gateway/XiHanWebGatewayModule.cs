#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebGatewayModule
// Guid:13d9d929-d3de-4447-972e-715992171319
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:34:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Logging;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.Serialization;
using XiHan.Framework.Traffic;
using XiHan.Framework.Traffic.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core;

namespace XiHan.Framework.Web.Gateway;

/// <summary>
/// 曦寒框架 Web 网关模块
/// </summary>
/// <remarks>
/// 职责：流量入口治理 + 路由决策 + 策略执行
/// 不负责：业务逻辑处理、规则管理
/// </remarks>
[DependsOn(
    typeof(XiHanWebCoreModule),
    typeof(XiHanTrafficModule),
    typeof(XiHanMultiTenancyModule),
    typeof(XiHanLoggingModule),
    typeof(XiHanSerializationModule))]
public class XiHanWebGatewayModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 注册灰度路由服务
        services.AddGrayRouting();
    }
}
