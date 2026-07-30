// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed.Abstracts;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Exceptions;
using XiHan.Framework.Workflow.Options;

namespace XiHan.Framework.Workflow.Engine;

/// <summary>
/// 实例执行锁获取器（引擎与人工任务服务共用的单写者锁协议）
/// </summary>
/// <remarks>
/// 持锁期间以过期时间三分之一为周期后台自动续期，避免长执行批次（HTTP/脚本/补偿链）超过锁过期时间后放入第二个写者。
/// </remarks>
internal static class WorkflowInstanceLocker
{
    /// <summary>
    /// 获取实例执行锁（超时未获取到抛出锁超时异常）
    /// </summary>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="options">工作流选项</param>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>自动续期的锁句柄</returns>
    /// <exception cref="WorkflowLockTimeoutException">获取锁超时时抛出</exception>
    public static async Task<IAsyncDisposable> AcquireAsync(
        IDistributedLock distributedLock,
        XiHanWorkflowOptions options,
        string instanceId,
        CancellationToken cancellationToken)
    {
        var resourceKey = WorkflowConsts.InstanceLockKeyPrefix + instanceId;
        var expiry = TimeSpan.FromSeconds(options.InstanceLockExpirySeconds);
        var deadline = DateTime.UtcNow.AddSeconds(options.InstanceLockAcquireTimeoutSeconds);

        while (true)
        {
            var handle = await distributedLock.TryAcquireAsync(resourceKey, expiry, cancellationToken);
            if (handle is not null)
            {
                return new AutoExtendingLockHandle(handle, expiry);
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new WorkflowLockTimeoutException(instanceId);
            }

            await Task.Delay(options.InstanceLockRetryIntervalMilliseconds, cancellationToken);
        }
    }

    /// <summary>
    /// 自动续期锁句柄（释放时先停止续期再释放底层锁）
    /// </summary>
    private sealed class AutoExtendingLockHandle : IAsyncDisposable
    {
        private readonly IDistributedLockHandle _handle;
        private readonly CancellationTokenSource _renewalCts = new();
        private readonly Task _renewalTask;

        public AutoExtendingLockHandle(IDistributedLockHandle handle, TimeSpan expiry)
        {
            _handle = handle;
            _renewalTask = RenewLoopAsync(handle, expiry, _renewalCts.Token);
        }

        public async ValueTask DisposeAsync()
        {
            await _renewalCts.CancelAsync();
            try
            {
                await _renewalTask;
            }
            catch (OperationCanceledException)
            {
                // 续期循环随取消结束
            }

            _renewalCts.Dispose();
            await _handle.DisposeAsync();
        }

        private static async Task RenewLoopAsync(IDistributedLockHandle handle, TimeSpan expiry, CancellationToken cancellationToken)
        {
            var interval = TimeSpan.FromMilliseconds(Math.Max(1000, expiry.TotalMilliseconds / 3));

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(interval, cancellationToken);

                try
                {
                    if (!await handle.ExtendAsync(expiry, cancellationToken))
                    {
                        // 锁已丢失（过期被他人获取），续期停止；后续冲突由存储层最后写入语义兜底
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // 单次续期失败继续尝试，锁自然过期前仍有机会续上
                }
            }
        }
    }
}
