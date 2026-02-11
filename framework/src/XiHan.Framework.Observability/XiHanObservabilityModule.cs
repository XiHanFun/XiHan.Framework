#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObservabilityModule
// Guid:eb325b31-d4a2-4fa2-8761-be838683f167
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 03:58:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
