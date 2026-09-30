// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 幂等键取得结果
/// </summary>
public sealed class IdempotencyAcquireResult
{
    private IdempotencyAcquireResult(IdempotencyAcquireStatus status, Guid ownerToken, StoredResponse? response)
    {
        Status = status;
        OwnerToken = ownerToken;
        Response = response;
    }

    /// <summary>
    /// 取得状态
    /// </summary>
    public IdempotencyAcquireStatus Status { get; }

    /// <summary>
    /// 拥有者令牌，仅 <see cref="IdempotencyAcquireStatus.Acquired"/> 时有效
    /// </summary>
    public Guid OwnerToken { get; }

    /// <summary>
    /// 已保存的响应，仅 <see cref="IdempotencyAcquireStatus.Replay"/> 时有值
    /// </summary>
    public StoredResponse? Response { get; }

    /// <summary>
    /// 已取得
    /// </summary>
    /// <param name="ownerToken">拥有者令牌</param>
    /// <returns>取得结果</returns>
    public static IdempotencyAcquireResult Acquired(Guid ownerToken)
    {
        return new(IdempotencyAcquireStatus.Acquired, ownerToken, null);
    }

    /// <summary>
    /// 重播已保存的响应
    /// </summary>
    /// <param name="response">响应快照</param>
    /// <returns>取得结果</returns>
    public static IdempotencyAcquireResult Replay(StoredResponse response)
    {
        return new(IdempotencyAcquireStatus.Replay, Guid.Empty, response);
    }

    /// <summary>
    /// 正在处理
    /// </summary>
    /// <returns>取得结果</returns>
    public static IdempotencyAcquireResult InProgress()
    {
        return new(IdempotencyAcquireStatus.InProgress, Guid.Empty, null);
    }

    /// <summary>
    /// 内容冲突
    /// </summary>
    /// <returns>取得结果</returns>
    public static IdempotencyAcquireResult Conflict()
    {
        return new(IdempotencyAcquireStatus.Conflict, Guid.Empty, null);
    }

    /// <summary>
    /// 结果不确定
    /// </summary>
    /// <returns>取得结果</returns>
    public static IdempotencyAcquireResult Indeterminate()
    {
        return new(IdempotencyAcquireStatus.Indeterminate, Guid.Empty, null);
    }

    /// <summary>
    /// 容量已满
    /// </summary>
    /// <returns>取得结果</returns>
    public static IdempotencyAcquireResult CapacityExceeded()
    {
        return new(IdempotencyAcquireStatus.CapacityExceeded, Guid.Empty, null);
    }
}
