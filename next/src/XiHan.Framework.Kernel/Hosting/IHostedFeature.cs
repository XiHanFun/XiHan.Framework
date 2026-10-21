// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 需要启动与停止的能力包入口。启动按拓扑正序，停止按逆序；每一步带取消与超时。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IHostedFeature : IFeature
{
    /// <summary>
    /// 启动。返回后特性进入 <see cref="LifecycleState.Ready"/>（或因可选依赖缺失进入 <see cref="LifecycleState.Degraded"/>）。
    /// </summary>
    Task StartAsync(IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// 停止。调用前特性已进入 <see cref="LifecycleState.Draining"/>。
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
