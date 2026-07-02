#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotBuilderWeComExtensions
// Guid:89c2ac45-51df-42f7-8fc9-f4094d199a46
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.WeCom.Abstractions;
using XiHan.Framework.Bot.WeCom.Messaging;
using XiHan.Framework.Bot.WeCom.Options;
using XiHan.Framework.Bot.WeCom.Stores;

namespace XiHan.Framework.Bot.WeCom.Extensions;

/// <summary>
/// BotBuilder 企业微信扩展
/// </summary>
public static class BotBuilderWeComExtensions
{
    /// <summary>
    /// 启用企业微信提供者
    /// </summary>
    /// <param name="builder">Bot 构建器</param>
    /// <param name="configure">企业微信配置委托</param>
    /// <returns>Bot 构建器</returns>
    public static BotBuilder UseWeCom(this BotBuilder builder, Action<WeComOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, WeComBotProvider>());
        builder.Services.TryAddSingleton<IWeComConfigStore, DefaultWeComConfigStore>();

        return builder;
    }
}
