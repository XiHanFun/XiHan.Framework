#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CacheEvents
// Guid:7c6d5e4f-3d2c-1b0a-9f8e-7d6c5b4a3b2c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
