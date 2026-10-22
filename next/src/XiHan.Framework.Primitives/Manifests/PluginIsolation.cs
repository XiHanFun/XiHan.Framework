// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 插件的隔离边界。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum PluginIsolation
{
    /// <summary>可收集 AssemblyLoadContext。</summary>
    InProcess = 0,

    /// <summary>WASI 组件。</summary>
    Wasm = 1,

    /// <summary>子进程 + gRPC 插件协议。</summary>
    OutOfProcess = 2,
}
