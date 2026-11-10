// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 启动时往写入器写一行标记的宿主服务
/// </summary>
/// <remarks>
/// 用来观察启动 Banner 相对其他宿主服务的先后
/// </remarks>
/// <param name="output">写入器</param>
/// <param name="marker">标记文本</param>
public sealed class MarkerHostedService(TextWriter output, string marker) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        output.WriteLine(marker);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
