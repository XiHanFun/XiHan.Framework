// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 接受连接但永远不回话的服务器
/// </summary>
/// <remarks>
/// 监听之后不取连接：握手由操作系统完成，客户端发出启动消息后就一直等回复，只有取消令牌或驱动自己的超时能让它结束
/// </remarks>
internal sealed class StalledTcpServer : IDisposable
{
    /// <summary>
    /// 监听器
    /// </summary>
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);

    /// <summary>
    /// 开始监听
    /// </summary>
    public StalledTcpServer() => _listener.Start();

    /// <summary>
    /// 指向本服务器的 PostgreSQL 连接串
    /// </summary>
    /// <remarks>
    /// 驱动自己的超时配得比用例的上限长：探测在上限之内结束，只能是取消令牌起了作用
    /// </remarks>
    public string PostgresConnectionString => $"Host=127.0.0.1;Port={((IPEndPoint)_listener.LocalEndpoint).Port};Username=stalled;Password=stalled;Database=stalled;Timeout=120;Pooling=false";

    /// <inheritdoc />
    public void Dispose() => _listener.Stop();
}
