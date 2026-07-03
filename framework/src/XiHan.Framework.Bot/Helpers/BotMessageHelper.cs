#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BotMessageHelper
// Guid:557a0335-a469-48d1-998f-feb7dfde6e09
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/11 19:03:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Helpers;

/// <summary>
/// 消息数据辅助类
/// </summary>
public static class BotMessageHelper
{
    /// <summary>
    /// 尝试从消息 Data 中获取指定键的强类型值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="message">消息</param>
    /// <param name="key">键名</param>
    /// <param name="value">获取到的值；不存在或类型不匹配时为默认值</param>
    /// <returns>是否获取成功</returns>
    public static bool TryGetData<T>(BotMessage message, string key, out T? value)
    {
        if (message.Data is not null && message.Data.TryGetValue(key, out var data) && data is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }
}
