#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLoggingModule
// Guid:b8f3e4d2-9c71-4a8b-b5d6-3e2f1a8c9d4e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 11:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Logging.Extensions.DependencyInjection;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging;

/// <summary>
/// 曦寒框架日志模块
/// </summary>
public class XiHanLoggingModule : XiHanModule
{
    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context"></param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 配置日志选项
        services.Configure<XiHanLoggingOptions>(options =>
        {
            // 设置默认配置
            options.IsEnabled = true;
            options.MinimumLevel = LogLevel.Information;
        });
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        // 添加 XiHan 日志服务
        services.AddXiHanLogging();
    }
}
