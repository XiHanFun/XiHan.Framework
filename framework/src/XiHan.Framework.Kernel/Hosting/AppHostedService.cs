// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 把 <see cref="App"/> 接入 Generic Host 的生命周期
/// </summary>
/// <remarks>
/// 应用在 <see cref="StartingAsync"/> 里启动、在 <see cref="StoppedAsync"/> 里停止：宿主先跑完全部 <c>StartingAsync</c> 才调用任何宿主服务的 <c>StartAsync</c>，
/// 所有 <c>StopAsync</c> 都返回之后才调用 <c>StoppedAsync</c>。所以启动核对与特性启动先于全部普通宿主服务（含 <see cref="BackgroundService"/>，不论登记在哪、宿主是否并发启停），
/// 它们开始时特性已经就绪，停止时特性还在；启动核对不过时它们一个都没有开始（ADR-0353；以前在 <c>StartAsync</c> / <c>StopAsync</c> 里，按登记顺序排在应用与特性登记的宿主服务之后）
/// </remarks>
/// <param name="app">应用实例</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "由容器通过 AddHostedService 实例化。")]
internal sealed class AppHostedService(App app) : IHostedLifecycleService
{
    /// <summary>
    /// 宿主启动的第一阶段执行启动核对并启动特性
    /// </summary>
    public Task StartingAsync(CancellationToken cancellationToken) => app.StartAsync(cancellationToken);

    /// <summary>
    /// 应用已在 <see cref="StartingAsync"/> 里启动，这里什么都不做
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 什么都不做
    /// </summary>
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 什么都不做
    /// </summary>
    /// <remarks>
    /// 这时其他宿主服务还在运行，特性不能先停
    /// </remarks>
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 应用留到 <see cref="StoppedAsync"/> 再停止，这里什么都不做
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 宿主停止的最后阶段按逆序停止特性
    /// </summary>
    public Task StoppedAsync(CancellationToken cancellationToken) => app.StopAsync(cancellationToken);
}
