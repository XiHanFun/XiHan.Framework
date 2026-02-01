#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueueOverflowPolicy
// Guid:404e5190-95e9-4eb9-9c18-b91beca52fc6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/18 06:14:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Logging;

/// <summary>
/// 队列溢出策略
/// </summary>
public enum QueueOverflowPolicy
{
    /// <summary>
    /// 丢弃低优先级日志（Info、Success、Handle）
    /// </summary>
    DropLowPriority,

    /// <summary>
    /// 阻塞等待（可能影响性能）
    /// </summary>
    Block,

    /// <summary>
    /// 丢弃最旧的日志
    /// </summary>
    DropOldest,

    /// <summary>
    /// 扩容队列（临时，有内存风险）
    /// </summary>
    Expand
}
