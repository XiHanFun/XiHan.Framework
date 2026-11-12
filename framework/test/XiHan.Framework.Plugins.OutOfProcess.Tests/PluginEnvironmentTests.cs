// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.OutOfProcess.Processes;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 传给插件进程的环境变量白名单（ADR-0208）
/// </summary>
/// <remarks>
/// 安全审查 S-38：原先按前缀 <c>GO</c> 放行，<c>GOOGLE_APPLICATION_CREDENTIALS</c> 等 Google 云凭据随之交给了插件
/// </remarks>
public sealed class PluginEnvironmentTests
{
    [Theory]
    [InlineData("PATH", true)]
    [InlineData("DOTNET_gcServer", true)]
    [InlineData("GOMAXPROCS", true)]
    [InlineData("GOMEMLIMIT", true)]
    [InlineData("GOGC", true)]
    [InlineData("GODEBUG", true)]
    [InlineData("GOTRACEBACK", true)]
    [InlineData("GOOGLE_APPLICATION_CREDENTIALS", false)]
    [InlineData("GOOGLE_CLOUD_PROJECT", false)]
    [InlineData("GOPATH", false)]
    [InlineData("AWS_SECRET_ACCESS_KEY", false)]
    public void OnlyListedVariables_AreInherited(string name, bool inherited)
        => Assert.Equal(inherited, PluginProcess.IsInherited(name));
}
