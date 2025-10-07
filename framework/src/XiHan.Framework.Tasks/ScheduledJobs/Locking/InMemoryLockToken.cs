#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryLockToken
// Guid:cbd506bf-ceec-4869-98a9-ee3775ec7af8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/7 16:46:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;

namespace XiHan.Framework.Tasks.ScheduledJobs.Locking;

/// <summary>
/// 内存锁令牌
/// </summary>
internal class InMemoryLockToken : ILockToken
{
    private readonly InMemoryLockProvider _provider;
    private bool _isReleased;

    public InMemoryLockToken(string resourceKey, string lockId, InMemoryLockProvider provider)
    {
        ResourceKey = resourceKey;
        LockId = lockId;
        _provider = provider;
    }

    public string ResourceKey { get; }
    public string LockId { get; }
    public bool IsReleased => _isReleased;

    /// <summary>
    /// 异步释放锁
    /// </summary>
    /// <returns></returns>
    public Task ReleaseAsync()
    {
        if (!_isReleased)
        {
            _provider.ReleaseLock(ResourceKey, LockId);
            _isReleased = true;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 同步释放锁
    /// </summary>
    public void Dispose()
    {
        ReleaseAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步释放锁
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
    }
}
