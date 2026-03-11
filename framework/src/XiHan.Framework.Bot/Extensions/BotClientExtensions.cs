#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotClientExtensions
// Guid:268ba95a-d592-49aa-ae9e-6828c18d04ac
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 17:47:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
