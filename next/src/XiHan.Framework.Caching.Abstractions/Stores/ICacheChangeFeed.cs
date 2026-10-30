// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Caching.Abstractions.Stores;

/// <summary>
/// 共享存储的变更通知。
/// </summary>
/// <remarks>
/// 可选能力：收到本实例与其他实例对存储的写入与删除，用来失效本地副本（两级缓存的 L1，ADR-0055）。
/// 本实例的变更在写入完成时同步通知，其他实例的经后端转发、不回送给发出者；通知可能丢失（断线期间），不是可靠的消息。处理器在写入的调用里同步执行，不应阻塞或抛出。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface ICacheChangeFeed
{
    /// <summary>
    /// 订阅变更。
    /// </summary>
    /// <remarks>
    /// 释放返回值即退订。
    /// </remarks>
    IDisposable Subscribe(Action<CacheChange> handler);
}
