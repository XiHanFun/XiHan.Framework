// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.Conformance;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Conformance;

/// <summary>
/// Go 写的示例插件 echo-go（framework-sdk-go）跑协议一致性套件；本机没有 go 时跳过。
/// </summary>
public sealed class EchoGoPluginConformanceTests : PluginProtocolConformance
{
    /// <inheritdoc />
    protected override string PluginDirectory
    {
        get
        {
            EchoGoPluginHost.EnsureBuiltOrSkip();
            return EchoGoPluginHost.Directory;
        }
    }

    /// <inheritdoc />
    protected override string ConfigurationJson => """{"greeting":"conformance"}""";

    /// <inheritdoc />
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.crash" };

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string>(StringComparer.Ordinal) { ["echoes.create"] = """{"text":"conformance"}""" };
}
