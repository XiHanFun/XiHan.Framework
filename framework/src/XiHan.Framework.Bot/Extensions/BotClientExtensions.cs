// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Clients;

namespace XiHan.Framework.Bot.Extensions;

/// <summary>
/// Bot 客户端扩展
/// </summary>
public static class BotClientExtensions
{
    /// <summary>
    /// 创建告警构建器
    /// </summary>
    public static BotAlertBuilder Alert(this IBotClient client)
    {
        return new BotAlertBuilder(client);
    }
}
