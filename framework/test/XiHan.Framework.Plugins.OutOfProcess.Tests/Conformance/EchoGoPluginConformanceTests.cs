// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.Conformance;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Conformance;

/// <summary>
/// Go 写的示例插件 echo-go（framework-sdk-go）跑协议一致性套件
/// </summary>
/// <remarks>
/// 本机没有 go 时跳过
/// </remarks>
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
    /// <remarks>
    /// 示例钉住的 Go SDK 版本还不校验会话令牌：SDK 发布校验令牌的版本、示例升级之后去掉这个覆写（ADR-0202）
    /// </remarks>
    protected override bool VerifiesSessionToken => false;

    /// <inheritdoc />
    protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.crash" };

    /// <inheritdoc />
    protected override IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string>(StringComparer.Ordinal) { ["echoes.create"] = """{"text":"conformance"}""" };
}
