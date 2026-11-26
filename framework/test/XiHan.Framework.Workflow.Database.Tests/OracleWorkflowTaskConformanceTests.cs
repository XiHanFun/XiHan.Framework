// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Conformance.Engine;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库工作流特性对 Oracle 通过用户任务与外部任务套件
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_ORACLE</c>，未设置即跳过（<see cref="OracleSchema"/>：每个用例新建用户与全部工作流表，宿主启动的期限放宽到 180 秒），由 CI 的 oracle 分片运行；与别的 Oracle 用例同在 <see cref="OracleGroup"/>，不并行。时间列是 <c>timestamp(6)</c>，与 PostgreSQL 同样到微秒。后台轮询周期设得很长，由用例主动 <c>PollAsync</c>
/// </remarks>
[Collection(nameof(OracleGroup))]
public sealed class OracleWorkflowTaskConformanceTests : WorkflowTaskConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowEngineHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var database = OracleSchema.Create();
        var host = await database.StartHostAsync(configuration, configure);
        var poller = host.Services.GetRequiredService<DueBookmarkPoller>();
        return new WorkflowEngineHost(host.Services, ct => poller.PollOnceAsync(100, ct), database);
    }
}
