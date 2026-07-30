// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Upgrade.Abstractions;

/// <summary>
/// 升级分布式锁提供者接口
/// </summary>
public interface IUpgradeLockProvider
{
    /// <summary>
    /// 尝试获取锁
    /// </summary>
    /// <param name="resourceKey"></param>
    /// <param name="expiry"></param>
    /// <param name="nodeName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IUpgradeLockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, string nodeName, CancellationToken cancellationToken = default);
}

/// <summary>
/// 升级锁令牌
/// </summary>
public interface IUpgradeLockToken : IAsyncDisposable
{
    /// <summary>
    /// 资源键
    /// </summary>
    string ResourceKey { get; }

    /// <summary>
    /// 锁标识
    /// </summary>
    string LockId { get; }

    /// <summary>
    /// 是否已释放
    /// </summary>
    bool IsReleased { get; }

    /// <summary>
    /// 释放锁
    /// </summary>
    Task ReleaseAsync();
}
