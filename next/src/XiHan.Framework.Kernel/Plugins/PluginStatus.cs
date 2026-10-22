// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 插件状态快照。
/// </summary>
/// <param name="Manifest">清单。</param>
/// <param name="State">生命周期状态。</param>
/// <param name="Error">失败原因。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record PluginStatus(Manifest Manifest, LifecycleState State, Error? Error = null);
