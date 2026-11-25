// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 查询列的回填在真实的 SQL Server 上（ADR-0304）
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_SQLSERVER</c>，未设置即跳过（<see cref="SqlServerDatabase"/>），由 CI 的 database 分片运行。SQL Server 的 <c>LIKE</c> 照库的排序规则（夹具取区分全宽与重音的版本 100 排序规则），时间是 <c>datetimeoffset</c>，到 100 纳秒
/// </remarks>
public sealed class SqlServerWorkflowBackfillTests : ServerWorkflowBackfillTests
{
    /// <inheritdoc />
    private protected override IGatedDatabase CreateDatabase() => SqlServerDatabase.Create();
}
