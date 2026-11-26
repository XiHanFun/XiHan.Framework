// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 查询列的回填在真实的 Oracle 上（ADR-0304）
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_ORACLE</c>，未设置即跳过（<see cref="OracleSchema"/>），由 CI 的 oracle 分片运行。Oracle 的 <c>LIKE</c> 按码元比较、长文本列是 <c>NCLOB</c>、空串存成 <c>NULL</c>，时间是 <c>timestamp(6)</c>
/// </remarks>
[Collection(nameof(OracleGroup))]
public sealed class OracleWorkflowBackfillTests : ServerWorkflowBackfillTests
{
    /// <inheritdoc />
    private protected override IGatedDatabase CreateDatabase() => OracleSchema.Create();
}
