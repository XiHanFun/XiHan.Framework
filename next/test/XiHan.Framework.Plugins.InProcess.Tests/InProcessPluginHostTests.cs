// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.InProcess.Tests;

/// <summary>
/// 进程内插件宿主的加载、隔离、权限与卸载验证；以已构建的 SamplePlugin 输出目录作为插件源。
/// </summary>
public sealed class InProcessPluginHostTests
{
    /// <summary>
    /// 授予示例插件清单里声明的全部权限。
    /// </summary>
    private static readonly PermissionGrant _granted = new(["capability:kernel.diagnostics"]);

    /// <summary>
    /// 示例插件的构建输出目录；与测试自身的配置（Debug/Release）保持一致。
    /// </summary>
    private static string PluginDirectory
    {
        get
        {
            // 测试输出在 .../bin/<Configuration>/net10.0/，示例插件在 samples/SamplePlugin/bin/<Configuration>/net10.0/
            var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            var configuration = output.Parent!.Name;
            var root = LocateRoot(output);
            return Path.Combine(root, "samples", "SamplePlugin", "bin", configuration, "net10.0");
        }
    }

    /// <summary>
    /// 以空配置与空日志创建宿主。
    /// </summary>
    private static InProcessPluginHost CreateHost() =>
        #pragma warning disable IL2026, IL3050 // 测试进程不裁剪、不 AOT
        new(new ConfigurationBuilder().Build(), NullLoggerFactory.Instance);
        #pragma warning restore IL2026, IL3050

    [Fact]
    public async Task Load_StartsPluginInIsolatedContext_AndUnloadStopsIt()
    {
        await using var host = CreateHost();

        var loaded = await host.LoadAsync(new PluginSource(PluginDirectory, _granted), CancellationToken.None);

        var status = loaded.ValueOrThrow();
        Assert.Equal("xihan.samples.plugin", status.Manifest.Id);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Single(host.Plugins);

        // 隔离的证据：带入口类型的 SamplePlugin 程序集不能出现在默认加载上下文里
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), static a => a.GetName().Name == "SamplePlugin" && a.GetType("SamplePlugin.SamplePlugin") is not null && System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(a) == System.Runtime.Loader.AssemblyLoadContext.Default);

        var duplicate = await host.LoadAsync(new PluginSource(PluginDirectory, _granted), CancellationToken.None);
        Assert.Equal(PluginErrors.AlreadyLoaded, duplicate.Error!.Code);

        var unloaded = await host.UnloadAsync("xihan.samples.plugin", CancellationToken.None);
        Assert.True(unloaded.IsSuccess);
        Assert.Empty(host.Plugins);
        Assert.Equal(PluginErrors.NotFound, (await host.UnloadAsync("xihan.samples.plugin", CancellationToken.None)).Error!.Code);
    }

    [Fact]
    public async Task Load_WithoutGrantedPermissions_IsDenied()
    {
        await using var host = CreateHost();

        var result = await host.LoadAsync(new PluginSource(PluginDirectory, PermissionGrant.Empty), CancellationToken.None);

        Assert.Equal(PluginErrors.PermissionDenied, result.Error!.Code);
        Assert.Empty(host.Plugins);
    }

    [Fact]
    public async Task Load_FromDirectoryWithoutManifest_ReportsInvalidSource()
    {
        await using var host = CreateHost();
        var empty = Path.Combine(Path.GetTempPath(), "xihan-plugin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var result = await host.LoadAsync(new PluginSource(empty, _granted), CancellationToken.None);

            Assert.Equal(PluginErrors.InvalidSource, result.Error!.Code);
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Fact]
    public async Task Load_WithMissingAssembly_ReportsNotFound()
    {
        await using var host = CreateHost();
        var directory = Path.Combine(Path.GetTempPath(), "xihan-plugin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // 只复制清单不复制程序集：清单合法、权限通过，但程序集文件缺失
            File.Copy(Path.Combine(PluginDirectory, "xihan.manifest.json"), Path.Combine(directory, "xihan.manifest.json"));

            var result = await host.LoadAsync(new PluginSource(directory, _granted), CancellationToken.None);

            Assert.Equal(PluginErrors.NotFound, result.Error!.Code);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_NonPluginManifest_ReportsInvalidSource()
    {
        await using var host = CreateHost();
        var directory = Path.Combine(Path.GetTempPath(), "xihan-plugin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // 把示例清单改成 capability 种类并去掉隔离与权限声明，模拟一个不是插件的清单
            var json = (await File.ReadAllTextAsync(Path.Combine(PluginDirectory, "xihan.manifest.json")))
                .Replace("\"kind\": \"plugin\"", "\"kind\": \"capability\"", StringComparison.Ordinal)
                .Replace("\"isolation\": \"in-process\",", string.Empty, StringComparison.Ordinal)
                .Replace("\"permissions\": [ \"capability:kernel.diagnostics\" ],", string.Empty, StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(directory, "xihan.manifest.json"), json);

            var result = await host.LoadAsync(new PluginSource(directory, _granted), CancellationToken.None);

            Assert.Equal(PluginErrors.InvalidSource, result.Error!.Code);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// 从测试输出目录向上查找 <c>XiHan.Framework.Next.slnx</c> 所在目录，即 <c>next/</c> 根。
    /// </summary>
    private static string LocateRoot(DirectoryInfo start)
    {
        var directory = start;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate 'XiHan.Framework.Next.slnx' in any parent directory of the test output.");
    }
}
