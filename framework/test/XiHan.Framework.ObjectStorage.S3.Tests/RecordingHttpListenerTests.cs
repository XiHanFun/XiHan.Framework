// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Sockets;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 回环假对端自身的释放
/// </summary>
/// <remarks>
/// 接受循环接住一条连接、还没回到下一次接受时释放：监听器已经停了，下一次接受抛 <see cref="InvalidOperationException"/>（Not listening），以前没接住，释放随之失败（第十轮 CI 的 integration 分片偶发）
/// </remarks>
public sealed class RecordingHttpListenerTests
{
    /// <summary>
    /// 反复的轮数
    /// </summary>
    /// <remarks>
    /// 竞态窗口很窄，单次碰不上；修复前本机每次运行都在 0.2 秒内复现
    /// </remarks>
    private const int Rounds = 500;

    [Fact]
    public async Task Dispose_RightAfterAConnectionArrives_EndsWithoutThrowing()
    {
        for (var round = 0; round < Rounds; round++)
        {
            var endpoint = RecordingHttpListener.Answering("200 OK");
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, endpoint.Port, TestContext.Current.CancellationToken);
            await endpoint.DisposeAsync();
        }
    }
}
