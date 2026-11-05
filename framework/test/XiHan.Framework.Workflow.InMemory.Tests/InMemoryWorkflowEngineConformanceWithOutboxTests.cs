// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 装了发件箱的宿主上，内存工作流特性照样通过引擎套件
/// </summary>
/// <remarks>
/// 发件箱以 <c>Replace</c> 换掉消息发布者，套件仍要看得到引擎发出的事件（ADR-0096 第六节）
/// </remarks>
public sealed class InMemoryWorkflowEngineConformanceWithOutboxTests : WorkflowEngineConformance
{
    /// <inheritdoc />
    /// <remarks>
    /// 内存工作流没有事务：写入立即生效，调用方回滚不撤销
    /// </remarks>
    protected override bool JoinsCallerTransaction => false;

    /// <inheritdoc />
    protected override Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
        => OutboxHost.StartAsync(configure, configuration);
}
