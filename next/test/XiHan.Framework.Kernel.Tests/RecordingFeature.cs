// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests;

/// <summary>
/// 记录生命周期事件的特性；可注入启动与停止行为。
/// </summary>
internal sealed class RecordingFeature(Manifest manifest, List<string> log) : IHostedFeature
{
    public Manifest Manifest { get; } = manifest;

    public Func<CancellationToken, Task>? OnStart { get; init; }

    public Func<CancellationToken, Task>? OnStop { get; init; }

    public Action<FeatureContext>? OnConfigure { get; init; }

    public void Configure(FeatureContext context)
    {
        log.Add($"configure:{Manifest.Id}");
        OnConfigure?.Invoke(context);
    }

    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        log.Add($"start:{Manifest.Id}");
        if (OnStart is not null)
        {
            await OnStart(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        log.Add($"stop:{Manifest.Id}");
        if (OnStop is not null)
        {
            await OnStop(cancellationToken);
        }
    }
}
