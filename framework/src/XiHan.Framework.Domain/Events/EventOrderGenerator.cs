#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventOrderGenerator
// Guid:68cb449d-661b-4f3e-bde9-388ea791198c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/10 5:45:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 事件顺序生成器
/// </summary>
public static class EventOrderGenerator
{
    private static long _lastOrder;

    /// <summary>
    /// 获取下一个事件顺序
    /// </summary>
    /// <returns></returns>
    public static long GetNext()
    {
        return Interlocked.Increment(ref _lastOrder);
    }
}
