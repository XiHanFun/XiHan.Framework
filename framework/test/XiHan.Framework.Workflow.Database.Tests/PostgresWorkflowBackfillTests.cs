// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 查询列的回填在真实的 PostgreSQL 上（ADR-0304）
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过（<see cref="PostgresDatabase"/>），由 CI 的 database 分片运行。PostgreSQL 的 <c>LIKE</c> 区分大小写、时间到微秒
/// </remarks>
public sealed class PostgresWorkflowBackfillTests : ServerWorkflowBackfillTests
{
    /// <inheritdoc />
    private protected override IGatedDatabase CreateDatabase() => PostgresDatabase.Create();
}
