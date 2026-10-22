// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.Conformance;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 示例插件 EchoPlugin 跑协议一致性套件。
/// </summary>
public sealed class EchoPluginConformanceTests : PluginProtocolConformance
{
    /// <inheritdoc />
    protected override string PluginDirectory => EchoPluginHost.Directory;

    /// <inheritdoc />
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.crash" };

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string>(StringComparer.Ordinal) { ["echoes.create"] = """{"text":"conformance"}""" };
}
