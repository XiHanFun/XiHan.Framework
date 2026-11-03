// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Hosting;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Hosting.CommandLine.Tests;

/// <summary>
/// 挂载配置目录在通用宿主上的位置
/// </summary>
/// <remarks>
/// <c>XIHAN_CONFIG_PATH</c> 目录里的文件插在 <c>appsettings.{环境}.json</c> 之后、环境变量之前。用例改进程环境变量（挂载目录与一个配置键），归入不并行的集合
/// </remarks>
[Collection(nameof(ProcessEnvironmentGroup))]
public sealed class ConfigurationDirectoryHostTests
{
    /// <summary>
    /// 用例里环境变量形式的配置键（<c>XiHanMountTest:FromEnvironment</c>）
    /// </summary>
    private const string EnvironmentKey = "XiHanMountTest__FromEnvironment";

    [Fact]
    public async Task AddXiHan_PlacesMountedDirectoryBetweenEnvironmentSettingsAndEnvironmentVariables()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "xihan-root-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(Path.GetTempPath(), "xihan-mount-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(contentRoot, "appsettings.json"), """{ "XiHanMountTest": { "Base": "appsettings", "Banner": "appsettings" } }""");
        await File.WriteAllTextAsync(Path.Combine(contentRoot, "appsettings.Staging.json"), """{ "XiHanMountTest": { "Banner": "staging", "FromEnvironment": "staging", "FromCommandLine": "staging" } }""");
        await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), """{ "XiHanMountTest": { "Banner": "mounted", "FromEnvironment": "mounted", "FromCommandLine": "mounted", "Override": "mounted" } }""");
        await File.WriteAllTextAsync(Path.Combine(directory, "override.json"), """{ "XiHanMountTest": { "Override": "override" } }""");
        Environment.SetEnvironmentVariable(ConfigurationDirectory.Variable, directory);
        Environment.SetEnvironmentVariable(EnvironmentKey, "environment");
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = ["--XiHanMountTest:FromCommandLine=command-line"],
            ContentRootPath = contentRoot,
            EnvironmentName = "Staging",
        });
        try
        {
            builder.AddXiHan(static _ => { });

            Assert.Equal("appsettings", builder.Configuration["XiHanMountTest:Base"]);
            Assert.Equal("mounted", builder.Configuration["XiHanMountTest:Banner"]);
            // 目录里的文件之间：appsettings.json 最先，其余按序数随后覆盖
            Assert.Equal("override", builder.Configuration["XiHanMountTest:Override"]);
            Assert.Equal("environment", builder.Configuration["XiHanMountTest:FromEnvironment"]);
            Assert.Equal("command-line", builder.Configuration["XiHanMountTest:FromCommandLine"]);
        }
        finally
        {
            // 释放配置源的文件监听，目录才能删掉
            builder.Configuration.Dispose();
            Environment.SetEnvironmentVariable(ConfigurationDirectory.Variable, null);
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
            Directory.Delete(directory, recursive: true);
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}
