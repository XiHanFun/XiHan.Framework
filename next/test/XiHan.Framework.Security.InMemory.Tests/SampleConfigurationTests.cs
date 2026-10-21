// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;

namespace XiHan.Framework.Security.InMemory.Tests;

/// <summary>
/// 示例应用的 appsettings.json 必须能被解析并用于认证：这是 Standard 示例"零外部基础设施运行"的一部分。
/// </summary>
public sealed class SampleConfigurationTests
{
    /// <summary>
    /// 从测试输出目录向上找到 next 工作区，定位示例应用的 appsettings.json。
    /// </summary>
    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The next workspace could not be located."), "samples", "SimpleApp", "appsettings.json");
    }

    [Fact]
    public void SimpleAppSettings_LoadPrincipalsAndGrants_AndAuthenticateByApiKey()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Locate(), optional: false).Build();

        var options = InMemorySecurityOptions.FromConfiguration(configuration);
        var store = new InMemoryPrincipalStore(options);

        Assert.Contains(options.Principals, static p => p.Id == "alice" && p.Tenant == "acme");
        Assert.Contains(options.Grants, static g => g.Subject.Id == "editor" && g.Permission == "orders.write");
        Assert.Equal("alice", store.FindByApiKey("dev-alice-key").ValueOrThrow().Id);
        Assert.Equal("bob", store.FindByApiKey("dev-bob-key").ValueOrThrow().Id);
        Assert.False(store.FindByApiKey("dev-alice-key ").HasValue);
    }
}
