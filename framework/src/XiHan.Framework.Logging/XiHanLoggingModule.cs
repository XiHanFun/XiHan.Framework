#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAspNetCoreSerilogModule
// Guid:b377c463-5f66-47c3-9b23-68e9c69b05cc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:50:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Serilog;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Logging;

/// <summary>
/// 曦寒框架 Web 核心 Serilog 模块
/// </summary>
public class XiHanLoggingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        Log.Logger = new XiHanLoggerBuilder().CreateLoggerDefault();

        _ = services.AddLogging(loggingBuilder =>
        {
            _ = loggingBuilder.AddSerilog();
        });
    }
}
