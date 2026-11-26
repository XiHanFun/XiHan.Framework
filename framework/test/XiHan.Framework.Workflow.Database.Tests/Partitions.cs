// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Conformance.Engine;
using XiHan.Framework.Workflow.Conformance.Storage;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 在测试宿主里以某个分区开一次调用、使用三个存储
/// </summary>
/// <remarks>
/// 存储按作用域在调用方的工作单元里读写，测试直接用存储时也要先有一次调用；复用一致性套件的夹具
/// </remarks>
internal static class Partitions
{
    /// <summary>
    /// 以 <paramref name="tenantId"/>（空为平台）开一次调用执行 <paramref name="action"/>，返回后提交
    /// </summary>
    public static Task<T> InAsync<T>(TestHost host, string? tenantId, Func<WorkflowStores, Task<T>> action)
        => new WorkflowStoreHost(host.Services, NoRelease.Instance).InPartitionAsync(tenantId, action);

    /// <summary>
    /// 没有返回值的 <see cref="InAsync{T}"/>
    /// </summary>
    public static Task InAsync(TestHost host, string? tenantId, Func<WorkflowStores, Task> action)
        => new WorkflowStoreHost(host.Services, NoRelease.Instance).InPartitionAsync(tenantId, action);

    /// <summary>
    /// 以 <paramref name="tenantId"/>（空为平台）开一次经管道的调用，在调用的服务里执行 <paramref name="action"/>，返回后提交
    /// </summary>
    /// <remarks>
    /// 目录与引擎加入这次调用的事务，取得的锁持有到调用结束；复用引擎套件的夹具
    /// </remarks>
    public static ValueTask<T> InvokeAsync<T>(TestHost host, string? tenantId, Func<IServiceProvider, ValueTask<T>> action)
        => new WorkflowEngineHost(host.Services, static _ => ValueTask.FromResult(0), NoRelease.Instance).AsTenantAsync(tenantId, action);

    /// <summary>
    /// 什么也不释放：宿主由测试自己释放
    /// </summary>
    private sealed class NoRelease : IAsyncDisposable
    {
        /// <summary>
        /// 唯一实例
        /// </summary>
        public static NoRelease Instance { get; } = new();

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
