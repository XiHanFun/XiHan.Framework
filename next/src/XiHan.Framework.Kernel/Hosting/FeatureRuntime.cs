// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 特性的运行时记录：实例、节点、状态机。
/// </summary>
/// <param name="node">本特性在已解析图中的节点。</param>
/// <param name="feature">特性实例。</param>
internal sealed class FeatureRuntime(ResolvedNode node, IFeature feature)
{
    /// <summary>
    /// 当前生命周期状态。创建时已完成 Configure，因此从 Configured 起步。
    /// </summary>
    private LifecycleState _state = LifecycleState.Configured;

    /// <summary>
    /// 本特性在已解析图中的节点。
    /// </summary>
    public ResolvedNode Node { get; } = node;

    /// <summary>
    /// 特性实例。
    /// </summary>
    public IFeature Feature { get; } = feature;

    /// <summary>
    /// 当前生命周期状态。
    /// </summary>
    public LifecycleState State => _state;

    /// <summary>
    /// 失败原因；未失败时为 <see langword="null"/>。
    /// </summary>
    public Error? Error { get; private set; }

    /// <summary>
    /// 当前状态快照。
    /// </summary>
    public FeatureStatus Status => new(Node.Manifest, _state, Error, Feature is IHostedFeature);

    /// <summary>
    /// 迁移到新状态；不合法的迁移抛出 <see cref="KernelErrors.InvalidAppState"/>，表示内核的状态机被误用。
    /// </summary>
    public void Transition(LifecycleState to)
    {
        if (!_state.CanTransitionTo(to))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, $"Feature '{Node.Manifest.Id}' cannot transition from '{_state}' to '{to}'.");
        }

        _state = to;
    }

    /// <summary>
    /// 记录失败原因并尽量进入 Failed；已处于终态或已失败时只更新原因，不改状态。
    /// </summary>
    public void Fail(Error error)
    {
        Error = error;
        if (_state.CanTransitionTo(LifecycleState.Failed))
        {
            _state = LifecycleState.Failed;
        }
    }
}
