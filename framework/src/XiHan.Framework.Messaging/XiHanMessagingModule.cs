// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Messaging.Extensions.DependencyInjection;

namespace XiHan.Framework.Messaging;

/// <summary>
/// 曦寒框架消息模块
/// </summary>
public class XiHanMessagingModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.AddXiHanMessaging();
    }
}
