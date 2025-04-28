#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventOrderGenerator
// Guid:b16caaf2-8972-4985-bd1b-9927fefb5db2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 5:29:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow;

/// <summary>
/// 事件顺序生成器
/// </summary>
public static class EventOrderGenerator
{
    private static long _lastOrder;

    /// <summary>
    /// 获取下一个顺序
    /// </summary>
    /// <returns></returns>
    public static long GetNext()
    {
        return Interlocked.Increment(ref _lastOrder);
    }
}
