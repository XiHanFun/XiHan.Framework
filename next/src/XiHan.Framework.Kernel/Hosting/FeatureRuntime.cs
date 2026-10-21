// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 特性的运行时记录：实例、节点、状态机。
/// </summary>
internal sealed class FeatureRuntime(ResolvedNode node, IFeature feature)
{
    private LifecycleState _state = LifecycleState.Configured;

    public ResolvedNode Node { get; } = node;

    public IFeature Feature { get; } = feature;

    public LifecycleState State => _state;

    public Error? Error { get; private set; }

    public FeatureStatus Status => new(Node.Manifest, _state, Error, Feature is IHostedFeature);

    public void Transition(LifecycleState to)
    {
        if (!_state.CanTransitionTo(to))
        {
            throw new FrameworkException(KernelErrors.InvalidAppState, $"特性 '{Node.Manifest.Id}' 不能从 {_state} 迁移到 {to}。");
        }

        _state = to;
    }

    public void Fail(Error error)
    {
        Error = error;
        if (_state.CanTransitionTo(LifecycleState.Failed))
        {
            _state = LifecycleState.Failed;
        }
    }
}
