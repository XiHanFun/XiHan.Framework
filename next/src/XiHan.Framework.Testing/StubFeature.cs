// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Testing;

/// <summary>
/// 记录生命周期事件、可注入行为的特性桩。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class StubFeature : IHostedFeature
{
    private readonly List<string> _events = [];

    /// <summary>
    /// 创建特性桩。
    /// </summary>
    public StubFeature(Manifest manifest)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    /// <inheritdoc />
    public Manifest Manifest { get; }

    /// <summary>
    /// 已记录的事件：<c>configure</c>、<c>start</c>、<c>stop</c>。
    /// </summary>
    public IReadOnlyList<string> Events => _events;

    /// <summary>
    /// 配置时执行。
    /// </summary>
    public Action<FeatureContext>? OnConfigure { get; init; }

    /// <summary>
    /// 启动时执行。
    /// </summary>
    public Func<IServiceProvider, CancellationToken, Task>? OnStart { get; init; }

    /// <summary>
    /// 停止时执行。
    /// </summary>
    public Func<CancellationToken, Task>? OnStop { get; init; }

    /// <inheritdoc />
    public void Configure(FeatureContext context)
    {
        _events.Add("configure");
        OnConfigure?.Invoke(context);
    }

    /// <inheritdoc />
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _events.Add("start");
        if (OnStart is not null)
        {
            await OnStart(services, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _events.Add("stop");
        if (OnStop is not null)
        {
            await OnStop(cancellationToken).ConfigureAwait(false);
        }
    }
}
