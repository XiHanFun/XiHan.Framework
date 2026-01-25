#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTrafficModule
// Guid:a7b8c9d0-e1f2-3a4b-5c6d-7e8f9a0b1c2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/1/22 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Traffic.Extensions.DependencyInjection;

namespace XiHan.Framework.Traffic;

/// <summary>
/// 曦寒框架流量治理模块
/// </summary>
/// <remarks>
/// 提供流量治理的基础抽象：灰度路由、限流、熔断等
/// 不负责具体执行，只提供规则模型和抽象接口
/// </remarks>
public class XiHanTrafficModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 默认注册灰度路由服务
        services.AddGrayRouting();
    }
}
