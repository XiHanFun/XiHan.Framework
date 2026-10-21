// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Pipeline;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 构建产物：已解析图、特性运行时、管道与选项。由 <see cref="App"/> 消费。
/// </summary>
/// <param name="Graph">已解析图。</param>
/// <param name="Features">已装配的特性运行时，按拓扑顺序；被顶替的节点不在其中。</param>
/// <param name="Pipeline">应用的管道构建器。</param>
/// <param name="Options">内核选项。</param>
/// <param name="Bindings">宿主解析出的绑定。</param>
internal sealed record AppModel(
    ResolvedGraph Graph,
    IReadOnlyList<FeatureRuntime> Features,
    PipelineBuilder Pipeline,
    KernelOptions Options,
    IReadOnlyDictionary<string, BindingValue> Bindings);
