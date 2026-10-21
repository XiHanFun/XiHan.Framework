// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Pipeline;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 构建产物：已解析图、特性运行时、管道与选项。由 <see cref="App"/> 消费。
/// </summary>
internal sealed record AppModel(
    ResolvedGraph Graph,
    IReadOnlyList<FeatureRuntime> Features,
    PipelineBuilder Pipeline,
    KernelOptions Options,
    IReadOnlyDictionary<string, BindingValue> Bindings);
