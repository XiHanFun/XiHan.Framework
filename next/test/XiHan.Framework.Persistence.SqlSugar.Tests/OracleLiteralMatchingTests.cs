// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// Oracle 上字符串按字面匹配。
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_ORACLE</c>，未设置即跳过。每个用例一个临时用户（schema）。Oracle 的 <c>LIKE</c> 区分大小写，没有缺省转义符；<c>ESCAPE</c> 指定的转义符后面只能跟 <c>%</c>、<c>_</c> 或它自己（否则 ORA-01424），约定在 Oracle 上不转义 <c>[</c>。
/// </remarks>
public sealed class OracleLiteralMatchingTests : SqlSugarLiteralMatchingTests, IDisposable
{
    /// <summary>
    /// 本测试实例独占的临时用户。
    /// </summary>
    /// <remarks>
    /// 首次访问时建出（未设置环境变量时在此跳过用例）。
    /// </remarks>
    private OracleSchema? _schema;

    /// <summary>
    /// 临时用户，按需创建。
    /// </summary>
    private OracleSchema Schema => _schema ??= OracleTestServer.CreateSchema();

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, string?>> Configuration => Schema.Configuration;

    /// <inheritdoc />
    protected override bool IgnoresCase => false;

    /// <inheritdoc />
    protected override IFeature CreateFeature()
    {
        _ = Schema;
        return OracleSchema.CreateFeature();
    }

    /// <summary>
    /// 测试结束后断开会话并删除用户。
    /// </summary>
    public void Dispose() => _schema?.Dispose();
}
