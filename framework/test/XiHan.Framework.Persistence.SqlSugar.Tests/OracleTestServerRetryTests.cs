// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;
using SqlSugar;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// Oracle 夹具建用户的管理连接失败时等两秒、清池再试一次
/// </summary>
/// <remarks>
/// 不需要 Oracle：管理连接指向一个只接受 TCP 连接、从不应答的本机端口，两次都按连接超时失败（ADR-0338、ADR-0350）。真实服务上的那类偶发由 CI 的 oracle 分片验证
/// </remarks>
public sealed class OracleTestServerRetryTests
{
    [Fact]
    public void CreatingTheUser_FailsAfterOneRetry_WithBothFailures()
    {
        using var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        var admin = $"User Id=system;Password=unused;Data Source=127.0.0.1:{((IPEndPoint)silent.LocalEndpoint).Port}/FREEPDB1;Connection Timeout=1";

        var failure = Assert.Throws<AggregateException>(() => OracleTestServer.CreateSchema(admin));

        // 只再试一次：两次的异常都在，不多也不少
        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.All(failure.InnerExceptions, static exception => Assert.IsType<SqlSugarException>(exception));
    }
}
