// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 需要启动/停止生命周期的特性。
/// 实现此接口的特性将在应用启动时调用 StartAsync，关闭时调用 StopAsync。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public interface IHostedFeature : IXiHanFeature
{
    /// <summary>
    /// 启动特性。
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止特性。
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
