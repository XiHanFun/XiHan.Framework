// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 记录生命周期事件的特性；可注入启动与停止行为。
/// </summary>
/// <param name="manifest">特性清单。</param>
/// <param name="log">事件日志，每个事件写一行 <c>configure:</c>、<c>start:</c> 或 <c>stop:</c> 加清单 ID。</param>
internal sealed class RecordingFeature(Manifest manifest, List<string> log) : IHostedFeature
{
    /// <summary>
    /// 特性清单。
    /// </summary>
    public Manifest Manifest { get; } = manifest;

    /// <summary>
    /// 启动时在记录事件之后执行的行为；用于注入失败或挂起。
    /// </summary>
    public Func<CancellationToken, Task>? OnStart { get; init; }

    /// <summary>
    /// 停止时在记录事件之后执行的行为；用于注入超时。
    /// </summary>
    public Func<CancellationToken, Task>? OnStop { get; init; }

    /// <summary>
    /// 配置时在记录事件之后执行的行为；用于注册服务、贡献中间件或读取绑定。
    /// </summary>
    public Action<FeatureContext>? OnConfigure { get; init; }

    /// <summary>
    /// 记录 <c>configure:</c> 事件并执行 <see cref="OnConfigure"/>。
    /// </summary>
    public void Configure(FeatureContext context)
    {
        log.Add($"configure:{Manifest.Id}");
        OnConfigure?.Invoke(context);
    }

    /// <summary>
    /// 记录 <c>start:</c> 事件并执行 <see cref="OnStart"/>。
    /// </summary>
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        log.Add($"start:{Manifest.Id}");
        if (OnStart is not null)
        {
            await OnStart(cancellationToken);
        }
    }

    /// <summary>
    /// 记录 <c>stop:</c> 事件并执行 <see cref="OnStop"/>。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        log.Add($"stop:{Manifest.Id}");
        if (OnStop is not null)
        {
            await OnStop(cancellationToken);
        }
    }
}
