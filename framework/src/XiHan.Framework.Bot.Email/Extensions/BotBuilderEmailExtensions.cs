#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotBuilderEmailExtensions
// Guid:4ad5ab05-0575-4e45-a1d0-e7736846ffa2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Messaging;
using XiHan.Framework.Bot.Email.Options;
using XiHan.Framework.Bot.Email.Stores;

namespace XiHan.Framework.Bot.Email.Extensions;

/// <summary>
/// BotBuilder 邮件扩展
/// </summary>
public static class BotBuilderEmailExtensions
{
    /// <summary>
    /// 启用邮件提供者
    /// </summary>
    /// <param name="builder">Bot 构建器</param>
    /// <param name="configure">邮件配置委托</param>
    /// <returns>Bot 构建器</returns>
    public static BotBuilder UseEmail(this BotBuilder builder, Action<EmailOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IBotProvider, EmailBotProvider>());
        builder.Services.TryAddSingleton<IEmailConfigStore, DefaultEmailConfigStore>();

        return builder;
    }
}
