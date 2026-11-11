// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 一个特性的状态快照
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record FeatureStatus
{
    /// <summary>
    /// 清单
    /// </summary>
    /// <exception cref="ArgumentNullException">为 <see langword="null"/></exception>
    public required Manifest Manifest
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// 生命周期状态
    /// </summary>
    public required LifecycleState State { get; init; }

    /// <summary>
    /// 失败原因
    /// </summary>
    /// <remarks>
    /// 没有失败为 <see langword="null"/>
    /// </remarks>
    public Error? Error { get; init; }

    /// <summary>
    /// 是否实现 <see cref="IHostedFeature"/>
    /// </summary>
    public required bool IsHosted { get; init; }
}
