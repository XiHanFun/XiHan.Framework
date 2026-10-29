// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 向应用健康贡献额外条目的服务（如插件宿主的每个插件）。
/// </summary>
/// <remarks>
/// 条目 <see cref="LifecycleState.Degraded"/> 或 <see cref="LifecycleState.Failed"/> 时应用报告为 Degraded，但不会因此退出或拒绝服务。注册为 <see cref="IHealthContributor"/> 单例即可被 <see cref="App.Health"/> 与 <see cref="App.State"/> 采集。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IHealthContributor
{
    /// <summary>
    /// 当前各条目的状态。
    /// </summary>
    /// <remarks>
    /// 必须快速返回且不抛出。
    /// </remarks>
    IReadOnlyList<ContributorStatus> Check();
}
