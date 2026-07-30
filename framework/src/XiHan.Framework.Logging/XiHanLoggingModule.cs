// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

        services.AddXiHanLogging(config);
    }
}
