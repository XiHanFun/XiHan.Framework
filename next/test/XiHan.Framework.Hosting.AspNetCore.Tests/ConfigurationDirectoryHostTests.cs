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
/// <c>XIHAN_CONFIG_PATH</c>：目录里的文件晚于 appsettings 装载并优先，文件变化触发重载。
/// </summary>
public sealed class ConfigurationDirectoryHostTests
{
    [Fact]
    public async Task AddXiHan_LoadsMountedDirectory_AndReloadsOnChange()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xihan-mount-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "appsettings.json");
        await File.WriteAllTextAsync(file, """{ "Sample": { "Banner": "mounted" } }""");
        Environment.SetEnvironmentVariable(ConfigurationDirectory.Variable, directory);
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Sample:Banner"] = "builtin", ["Sample:Other"] = "kept" });
            builder.AddXiHan(static xihan => xihan.UseFeature(new StubFeature(TestManifests.Create("xihan.test.mount"))));
            await using var app = builder.Build();
            await app.StartAsync();

            Assert.Equal("mounted", app.Configuration["Sample:Banner"]);
            Assert.Equal("kept", app.Configuration["Sample:Other"]);

            var reloaded = new TaskCompletionSource();
            ChangeToken.OnChange(app.Configuration.GetReloadToken, () => reloaded.TrySetResult());
            await File.WriteAllTextAsync(file, """{ "Sample": { "Banner": "updated" } }""");
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal("updated", app.Configuration["Sample:Banner"]);
            await app.StopAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConfigurationDirectory.Variable, null);
            Directory.Delete(directory, recursive: true);
        }
    }
}
