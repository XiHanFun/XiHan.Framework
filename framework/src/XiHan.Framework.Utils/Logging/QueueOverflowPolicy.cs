// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
