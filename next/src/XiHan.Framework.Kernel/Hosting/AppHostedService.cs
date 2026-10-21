// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 把 <see cref="App"/> 接入 Generic Host 的生命周期。
/// </summary>
/// <param name="app">应用实例。</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "由容器通过 AddHostedService 实例化。")]
internal sealed class AppHostedService(App app) : IHostedService
{
    /// <summary>
    /// 宿主启动时启动应用。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) => app.StartAsync(cancellationToken);

    /// <summary>
    /// 宿主停止时停止应用。
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken) => app.StopAsync(cancellationToken);
}
