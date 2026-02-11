#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanWebRealTimeModule
// Guid:2c8a0444-ea76-40c1-8f80-8e066469952d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/06 03:50:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Authentication;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Core;
using XiHan.Framework.Web.RealTime.Extensions;
using XiHan.Framework.Web.RealTime.Options;

namespace XiHan.Framework.Web.RealTime;

/// <summary>
/// 曦寒框架 Web 核心实时通信模块
/// </summary>
[DependsOn(
    typeof(XiHanWebCoreModule),
    typeof(XiHanAuthenticationModule)
    )]
public class XiHanWebRealTimeModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var config = services.GetConfiguration();

        var signalROptions = new XiHanSignalROptions();

        // 配置 SignalR 选项
        services.Configure<XiHanSignalROptions>(options =>
        {
            // 从配置文件读取
            config.GetSection(XiHanSignalROptions.SectionName).Bind(options);
        });

        // 添加 SignalR 服务
        config.GetSection(XiHanSignalROptions.SectionName).Bind(signalROptions);

        services.AddXiHanSignalRWithJson();
    }
}
