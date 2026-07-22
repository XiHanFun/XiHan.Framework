// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Auditing.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security;

namespace XiHan.Framework.Auditing;

/// <summary>
/// 曦寒框架审计日志模块
/// </summary>
/// <remarks>
/// 提供与传输/ORM 无关的审计日志采集基础设施：6 类日志记录（操作/访问/登录/异常/接口/实体变更）、
/// Channel 异步队列 + 后台消费者、采集管道、脱敏器、以及写入器契约（默认空实现）。
/// 应用侧实现各 <c>IXxxLogWriter</c> 与 <c>IEntityAuditContextProvider</c> 即可将日志落库。
/// </remarks>
[DependsOn(
    typeof(XiHanSecurityModule),
    typeof(XiHanMultiTenancyAbstractionsModule)
)]
public class XiHanAuditingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanAuditing(config);
    }
}
