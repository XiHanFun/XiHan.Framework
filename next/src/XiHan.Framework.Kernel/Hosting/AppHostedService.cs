// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 把 <see cref="App"/> 接入 Generic Host 的生命周期。
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "由容器通过 AddHostedService 实例化。")]
internal sealed class AppHostedService(App app) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => app.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => app.StopAsync(cancellationToken);
}
