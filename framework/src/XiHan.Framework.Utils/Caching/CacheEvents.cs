// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Caching;

/// <summary>
/// 缓存事件参数
/// </summary>
public class CacheEventArgs : EventArgs
{
    /// <summary>
    /// 缓存键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 事件类型
    /// </summary>
    public CacheEventType EventType { get; set; }

    /// <summary>
    /// 事件发生时间
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
