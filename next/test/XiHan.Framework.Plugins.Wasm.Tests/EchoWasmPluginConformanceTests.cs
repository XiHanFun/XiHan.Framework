// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.Conformance;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// 示例插件 EchoWasmPlugin 跑 Wasm 载体的一致性套件。
/// </summary>
public sealed class EchoWasmPluginConformanceTests : WasmPluginConformance
{
    /// <inheritdoc />
    protected override string PluginDirectory => EchoWasmPluginHost.Directory;

    /// <inheritdoc />
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.trap", "echoes.spin" };

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string>(StringComparer.Ordinal) { ["echoes.create"] = """{"text":"conformance"}""" };
}
