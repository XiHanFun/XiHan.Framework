// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions.Storage;
using Xunit;

namespace XiHan.Framework.Workflow.Conformance.Storage;

/// <summary>
/// 存储套件的夹具：装配了被测 Provider 的宿主，以及释放它的句柄。
/// </summary>
/// <remarks>
/// 存储按作用域在调用方的事务里读写：<see cref="InPartitionAsync{T}"/> 在新作用域里以给定租户开一次经管道的调用，在里面解析三个存储交给用例，用例返回后调用提交。租户预先写进调用的安全上下文，宿主不必装配多租户。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WorkflowStoreHost : IAsyncDisposable
{
    /// <summary>
    /// 释放句柄。
    /// </summary>
    private readonly IAsyncDisposable _host;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <param name="services">已启动的宿主的根服务提供者。</param>
    /// <param name="host">释放宿主的句柄。释放时先停宿主（应用排空：轮询器释放领导权、后台循环退出），再删库或删库文件；先删库再停，还在服务的宿主会如实报存储不可达（<c>WORKFLOW_UNAVAILABLE_001</c>）。</param>
    public WorkflowStoreHost(IServiceProvider services, IAsyncDisposable host)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>
    /// 根服务提供者。
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// 实例锁（单例，与事务无关）。
    /// </summary>
    public IWorkflowInstanceLock Lock => Services.GetRequiredService<IWorkflowInstanceLock>();

    /// <summary>
    /// 以 <paramref name="tenantId"/>（空为平台）开一次调用，在里面对三个存储执行 <paramref name="action"/> 后提交。
    /// </summary>
    /// <remarks>
    /// 用例里的断言失败不交给管道翻译成意外错误：记下来在调用之外原样抛出；调用本身失败（提交失败）以断言失败报告。
    /// </remarks>
    public async Task<T> InPartitionAsync<T>(string? tenantId, Func<WorkflowStores, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var scope = Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        if (tenantId is not null)
        {
            context.SetTenant(new TenantContext(tenantId));
        }

        var outcome = default(T);
        ExceptionDispatchInfo? failure = null;
        var invoked = await Services.GetRequiredService<App>().InvokeAsync(context, async invocation =>
        {
            try
            {
                var stores = new WorkflowStores(
                    tenantId,
                    invocation.Services.GetRequiredService<IWorkflowDefinitionStore>(),
                    invocation.Services.GetRequiredService<IWorkflowInstanceStore>(),
                    invocation.Services.GetRequiredService<IWorkflowBookmarkStore>());
                outcome = await action(stores).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        }).ConfigureAwait(false);
        failure?.Throw();
        Assert.True(invoked.IsSuccess, invoked.Error?.Code);
        return outcome!;
    }

    /// <summary>
    /// 没有返回值的 <see cref="InPartitionAsync{T}"/>。
    /// </summary>
    public Task InPartitionAsync(string? tenantId, Func<WorkflowStores, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return InPartitionAsync(tenantId, async stores =>
        {
            await action(stores).ConfigureAwait(false);
            return true;
        });
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
