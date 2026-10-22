// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 一个健康贡献条目。
/// </summary>
/// <param name="Name">条目名（如 <c>plugins.xihan.samples.echo</c>）。</param>
/// <param name="State">条目状态；只有 <see cref="LifecycleState.Degraded"/> 与 <see cref="LifecycleState.Failed"/> 影响应用聚合状态。</param>
/// <param name="Error">Degraded / Failed 的原因；可空。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ContributorStatus(string Name, LifecycleState State, Error? Error = null)
{
    /// <summary>
    /// 条目名。
    /// </summary>
    public string Name { get; } = !string.IsNullOrWhiteSpace(Name) ? Name : throw new ArgumentException("The contributor name must not be empty.", nameof(Name));
}
