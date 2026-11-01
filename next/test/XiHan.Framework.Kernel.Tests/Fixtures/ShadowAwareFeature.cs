// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 被顶替时仍登记服务、并记录生命周期事件的特性。
/// </summary>
/// <param name="manifest">特性清单。</param>
/// <param name="log">事件日志，每个事件写一行 <c>configure:</c>、<c>shadowed:</c>、<c>start:</c> 或 <c>stop:</c> 加清单 ID。</param>
internal sealed class ShadowAwareFeature(Manifest manifest, List<string> log) : IHostedFeature, IShadowAwareFeature
{
    /// <summary>
    /// 特性清单。
    /// </summary>
    public Manifest Manifest { get; } = manifest;

    /// <summary>
    /// 被顶替时在记录事件之后执行的行为。
    /// </summary>
    public Action<FeatureContext>? OnShadowed { get; init; }

    /// <summary>
    /// 记录 <c>configure:</c> 事件。
    /// </summary>
    public void Configure(FeatureContext context) => log.Add($"configure:{Manifest.Id}");

    /// <summary>
    /// 记录 <c>shadowed:</c> 事件并执行 <see cref="OnShadowed"/>。
    /// </summary>
    public void ConfigureShadowed(FeatureContext context)
    {
        log.Add($"shadowed:{Manifest.Id}");
        OnShadowed?.Invoke(context);
    }

    /// <summary>
    /// 记录 <c>start:</c> 事件。
    /// </summary>
    public Task StartAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        log.Add($"start:{Manifest.Id}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 记录 <c>stop:</c> 事件。
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        log.Add($"stop:{Manifest.Id}");
        return Task.CompletedTask;
    }
}
