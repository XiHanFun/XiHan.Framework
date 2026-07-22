// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
