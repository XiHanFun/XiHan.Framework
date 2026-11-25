// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 查询列的回填在真实的 MySQL 上（ADR-0304）
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_MYSQL</c>，未设置即跳过（<see cref="MySqlDatabase"/>），由 CI 的 database 分片运行。MySQL 的 <c>LIKE</c> 照库的排序规则（夹具取二进制排序规则），时间是 <c>datetime(6)</c>，到微秒、不带偏移
/// </remarks>
public sealed class MySqlWorkflowBackfillTests : ServerWorkflowBackfillTests
{
    /// <inheritdoc />
    private protected override IGatedDatabase CreateDatabase() => MySqlDatabase.Create();
}
