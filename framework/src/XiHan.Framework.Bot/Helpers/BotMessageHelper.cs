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

using System;
using System.Collections.Generic;
using System.Text;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.Helpers;

internal static class BotMessageHelper
{
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
