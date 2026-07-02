#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotBuilderDingTalkExtensions
// Guid:c351804a-6f6a-44db-9a8d-dbdc8038507e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.DingTalk.Abstractions;
using XiHan.Framework.Bot.DingTalk.Messaging;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.DingTalk.Stores;

namespace XiHan.Framework.Bot.DingTalk.Extensions;

/// <summary>
/// BotBuilder 钉钉扩展
/// </summary>
public static class BotBuilderDingTalkExtensions
{
    /// <summary>
    /// 启用钉钉提供者
    /// </summary>
    /// <param name="builder">Bot 构建器</param>
    /// <param name="configure">钉钉配置委托</param>
    /// <returns>Bot 构建器</returns>
    public static BotBuilder UseDingTalk(this BotBuilder builder, Action<DingTalkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, DingTalkBotProvider>());
        builder.Services.TryAddSingleton<IDingTalkConfigStore, DefaultDingTalkConfigStore>();

        return builder;
    }
}
