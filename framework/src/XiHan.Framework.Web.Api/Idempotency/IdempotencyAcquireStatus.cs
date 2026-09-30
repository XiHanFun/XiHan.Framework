// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 幂等键取得结果状态
/// </summary>
public enum IdempotencyAcquireStatus
{
    /// <summary>
    /// 已取得，调用方可执行动作
    /// </summary>
    Acquired,

    /// <summary>
    /// 已完成，重播已保存的响应
    /// </summary>
    Replay,

    /// <summary>
    /// 相同键的请求正在处理
    /// </summary>
    InProgress,

    /// <summary>
    /// 相同键已用于内容不同的请求
    /// </summary>
    Conflict,

    /// <summary>
    /// 结果不确定，拒绝自动重跑
    /// </summary>
    Indeterminate,

    /// <summary>
    /// 存储容量已满
    /// </summary>
    CapacityExceeded
}
