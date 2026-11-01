// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 挂载配置目录在 ASP.NET Core 宿主上的位置与重载。
/// </summary>
/// <remarks>
/// <c>XIHAN_CONFIG_PATH</c> 目录里的文件插在 <c>appsettings.{环境}.json</c> 之后、环境变量之前，文件变化触发重载。用例改进程环境变量（挂载目录与一个配置键），归入不并行的集合。
/// </remarks>
[Collection(nameof(ProcessEnvironmentGroup))]
public sealed class ConfigurationDirectoryHostTests
{
    /// <summary>
    /// 用例里环境变量形式的配置键（<c>XiHanMountTest:FromEnvironment</c>）。
    /// </summary>
    private const string EnvironmentKey = "XiHanMountTest__FromEnvironment";

    [Fact]
    public async Task AddXiHan_PlacesMountedDirectoryBetweenEnvironmentSettingsAndEnvironmentVariables_AndReloadsOnChange()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "xihan-root-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(Path.GetTempPath(), "xihan-mount-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(contentRoot, "appsettings.json"), """{ "XiHanMountTest": { "Base": "appsettings", "Banner": "appsettings" } }""");
        await File.WriteAllTextAsync(Path.Combine(contentRoot, "appsettings.Staging.json"), """{ "XiHanMountTest": { "Banner": "staging", "FromEnvironment": "staging", "FromCommandLine": "staging" } }""");
        var file = Path.Combine(directory, "appsettings.json");
        await File.WriteAllTextAsync(file, """{ "XiHanMountTest": { "Banner": "mounted", "FromEnvironment": "mounted", "FromCommandLine": "mounted", "FromApplication": "mounted" } }""");
        Environment.SetEnvironmentVariable(ConfigurationDirectory.Variable, directory);
        Environment.SetEnvironmentVariable(EnvironmentKey, "environment");
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = ["--XiHanMountTest:FromCommandLine=command-line"],
                ContentRootPath = contentRoot,
                EnvironmentName = "Staging",
            });
            builder.WebHost.UseTestServer();
            // 应用在 AddXiHan 之前自己加的配置源排在默认配置源之后，同样覆盖挂载目录
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["XiHanMountTest:FromApplication"] = "application" });
            builder.AddXiHan(static xihan => xihan.UseFeature(new StubFeature(TestManifests.Create("xihan.test.mount"))));
            await using var app = builder.Build();
            await app.StartAsync();

            Assert.Equal("appsettings", app.Configuration["XiHanMountTest:Base"]);
            Assert.Equal("mounted", app.Configuration["XiHanMountTest:Banner"]);
            Assert.Equal("environment", app.Configuration["XiHanMountTest:FromEnvironment"]);
            Assert.Equal("command-line", app.Configuration["XiHanMountTest:FromCommandLine"]);
            Assert.Equal("application", app.Configuration["XiHanMountTest:FromApplication"]);

            var reloaded = new TaskCompletionSource();
            ChangeToken.OnChange(app.Configuration.GetReloadToken, () => reloaded.TrySetResult());
            await File.WriteAllTextAsync(file, """{ "XiHanMountTest": { "Banner": "updated" } }""");
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal("updated", app.Configuration["XiHanMountTest:Banner"]);
            await app.StopAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationDirectory.Variable, null);
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
            Directory.Delete(directory, recursive: true);
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}
