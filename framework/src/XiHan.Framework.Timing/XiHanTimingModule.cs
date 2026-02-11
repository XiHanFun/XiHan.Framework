#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTimingModule
// Guid:8a9c5d4e-2f3a-4b7c-9e1d-6a8b4c3e2f1a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/11 04:56:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Timing.Extensions.DependencyInjection;

namespace XiHan.Framework.Timing;

/// <summary>
/// 曦寒框架时间管理模块
/// </summary>
public class XiHanTimingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        services.AddXiHanTiming();
    }
}
