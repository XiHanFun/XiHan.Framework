#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTimingModule
// Guid:8a9c5d4e-2f3a-4b7c-9e1d-6a8b4c3e2f1a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/1/11 4:56:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Timing;

/// <summary>
/// 曦寒框架时间管理模块
/// </summary>
[DependsOn(
    )]
public class XiHanTimingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<XiHanClockOptions>();

        context.Services.TryAddSingleton<IClock, Clock>();
        context.Services.TryAddSingleton<ITimezoneProvider, TZConvertTimezoneProvider>();
        context.Services.TryAddTransient<ICurrentTimezoneProvider, CurrentTimezoneProvider>();
    }
}
