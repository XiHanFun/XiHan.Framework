// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Api.Idempotency;

/// <summary>
/// 幂等记录存储
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// 原子取得幂等键：不存在则以处理中状态写入并返回拥有者令牌，存在则按其状态返回
    /// </summary>
    /// <param name="key">记录键</param>
    /// <param name="fingerprint">请求摘要</param>
    /// <param name="isTransactional">动作是否在事务型工作单元内执行</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>取得结果</returns>
    Task<IdempotencyAcquireResult> TryAcquireAsync(IdempotencyRecordKey key, string fingerprint, bool isTransactional, CancellationToken cancellationToken = default);

    /// <summary>
    /// 把拥有者持有的处理中记录写为已完成并保存响应快照
    /// </summary>
    /// <param name="key">记录键</param>
    /// <param name="ownerToken">拥有者令牌</param>
    /// <param name="response">响应快照</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    /// <exception cref="InvalidOperationException">记录不存在、令牌不符、已不是处理中状态，或存储的响应快照容量已满（记录保持处理中）</exception>
    Task CompleteAsync(IdempotencyRecordKey key, Guid ownerToken, StoredResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// 放弃取得，允许相同键重新执行
    /// </summary>
    /// <param name="key">记录键</param>
    /// <param name="ownerToken">拥有者令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task ReleaseAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 把拥有者持有的处理中记录标记为结果不确定
    /// </summary>
    /// <param name="key">记录键</param>
    /// <param name="ownerToken">拥有者令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task MarkIndeterminateAsync(IdempotencyRecordKey key, Guid ownerToken, CancellationToken cancellationToken = default);
}
