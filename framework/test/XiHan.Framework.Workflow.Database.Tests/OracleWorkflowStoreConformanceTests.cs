// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Workflow.Conformance.Storage;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 数据库存储与租约锁对 Oracle 通过存储套件
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_ORACLE</c>，未设置即跳过（<see cref="OracleSchema"/>），由 CI 的 oracle 分片运行
/// </remarks>
public sealed class OracleWorkflowStoreConformanceTests : WorkflowStoreConformance
{
    /// <inheritdoc />
    protected override async Task<WorkflowStoreHost> StartAsync()
    {
        var database = OracleSchema.Create();
        var host = await database.StartHostAsync();
        return new WorkflowStoreHost(host.Services, database);
    }
}
