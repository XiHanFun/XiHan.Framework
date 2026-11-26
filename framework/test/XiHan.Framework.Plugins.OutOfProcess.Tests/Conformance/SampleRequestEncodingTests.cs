// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Plugins.Conformance;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Conformance;

/// <summary>
/// 进程外插件的一致性套件把示例请求严格编码成 UTF-8
/// </summary>
/// <remarks>
/// 派生类给的示例请求有孤立的代理项时，用例按配置错误失败，不换成 U+FFFD 发出另一份请求（RFC-0001「写出与转交」，ADR-0327）
/// </remarks>
public sealed class SampleRequestEncodingTests
{
    [Fact]
    public async Task SampleWithALoneSurrogate_FailsTheCase_AsAConfigurationError()
    {
        var suite = new LoneSurrogateSuite();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(suite.Invoke_DeclaredOperations_ReturnAStatus);

        Assert.Contains("'echoes.create'", exception.Message, StringComparison.Ordinal);
        Assert.IsType<EncoderFallbackException>(exception.InnerException);
    }

    /// <summary>
    /// 给 <c>echoes.create</c> 一份带孤立代理项的示例请求的套件
    /// </summary>
    /// <remarks>
    /// 嵌套的私有类，xUnit 不把它当测试类发现，只由上面的用例调用
    /// </remarks>
    private sealed class LoneSurrogateSuite : PluginProtocolConformance
    {
        /// <inheritdoc />
        protected override string PluginDirectory => EchoPluginHost.Directory;

        /// <inheritdoc />
        protected override IReadOnlySet<string> ExcludedOperations => new HashSet<string>(StringComparer.Ordinal) { "echoes.crash" };

        /// <inheritdoc />
        protected override IReadOnlyDictionary<string, string> SampleRequests => new Dictionary<string, string>(StringComparer.Ordinal) { ["echoes.create"] = "{\"text\":\"\uD800\"}" };
    }
}
