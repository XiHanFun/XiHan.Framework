// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 本次请求取得的幂等执行上下文，内外两层过滤器经 HttpContext.Items 共享
/// </summary>
public sealed class IdempotencyExecution
{
    /// <summary>
    /// HttpContext.Items 中的键
    /// </summary>
    public static readonly object ItemKey = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="key">记录键</param>
    /// <param name="ownerToken">拥有者令牌</param>
    /// <param name="isTransactional">动作是否在事务型工作单元内执行</param>
    public IdempotencyExecution(IdempotencyRecordKey key, Guid ownerToken, bool isTransactional)
    {
        Key = key;
        OwnerToken = ownerToken;
        IsTransactional = isTransactional;
    }

    /// <summary>
    /// 记录键
    /// </summary>
    public IdempotencyRecordKey Key { get; }

    /// <summary>
    /// 拥有者令牌
    /// </summary>
    public Guid OwnerToken { get; }

    /// <summary>
    /// 动作是否在事务型工作单元内执行
    /// </summary>
    public bool IsTransactional { get; }

    /// <summary>
    /// 内层过滤器是否已写入完成
    /// </summary>
    public bool IsCompleted { get; set; }
}
