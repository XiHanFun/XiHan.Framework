// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing.Workspace;

namespace XiHan.Framework.Plugins.InProcess.Tests;

/// <summary>
/// 进程内插件宿主的加载、隔离、权限与卸载验证
/// </summary>
/// <remarks>
/// 以已构建的 SamplePlugin 输出目录作为插件源
/// </remarks>
public sealed class InProcessPluginHostTests
{
    /// <summary>
    /// 授予示例插件清单里声明的全部权限
    /// </summary>
    private static readonly PluginPermissions _granted = new(["capability:kernel.diagnostics"]);

    /// <summary>
    /// 示例插件的构建输出目录
    /// </summary>
    /// <remarks>
    /// 与测试自身的配置（Debug/Release）保持一致
    /// </remarks>
    private static string PluginDirectory
    {
        get
        {
            // 测试输出在 .../bin/<Configuration>/net10.0/，示例插件在 samples/SamplePlugin/bin/<Configuration>/net10.0/
            var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            var configuration = output.Parent!.Name;
            return Path.Combine(FrameworkWorkspace.Root, "samples", "SamplePlugin", "bin", configuration, "net10.0");
        }
    }

    /// <summary>
    /// 以空配置与空日志创建宿主
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

        var result = await host.LoadAsync(new PluginSource(PluginDirectory, PluginPermissions.Empty), CancellationToken.None);

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

    [Fact]
    public async Task Load_ManifestContributingMiddleware_IsRejected_InsteadOfIgnoringIt()
    {
        await using var host = CreateHost();
        var directory = Path.Combine(Path.GetTempPath(), "xihan-plugin-" + Guid.NewGuid().ToString("N"));
        try
        {
            // 完整的示例插件副本，只在清单里加一项中间件贡献：不能照常加载、把这一项静默忽略（PluginContext.Pipeline 恒为空）
            CopyTree(PluginDirectory, directory);
            var manifestPath = Path.Combine(directory, "xihan.manifest.json");
            var json = await File.ReadAllTextAsync(manifestPath);
            var edited = json.Replace("\"permissions\": [", "\"contributes\": { \"pipeline\": [ { \"middleware\": \"sample-guard\", \"stage\": \"observe\" } ] },\n  \"permissions\": [", StringComparison.Ordinal);
            Assert.NotEqual(json, edited);
            await File.WriteAllTextAsync(manifestPath, edited);

            var result = await host.LoadAsync(new PluginSource(directory, _granted), CancellationToken.None);

            // 只有 Wasm 插件能贡献中间件：在加载任何代码之前拒绝，不登记
            Assert.True(result.IsFailure, "A manifest contributing middleware must not load as an in-process plugin.");
            Assert.Equal(PluginErrors.InvalidSource, result.Error!.Code);
            Assert.Equal(PluginMessages.PipelineUnsupported, result.Error.MessageKey);
            Assert.Equal("sample-guard", result.Error.Details!["middleware"]);
            Assert.Empty(host.Plugins);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 断言失败、插件程序集已被加载时文件仍被占用：临时目录留给系统清理，不掩盖断言本身
            }
        }
    }

    [Theory]
    [InlineData("", "binding 'db' is required but not provided")]
    [InlineData("""XiHan:Bindings:db:type=queue""", "binding 'db' is required but not provided")]
    [InlineData("""XiHan:Bindings:db:type=connection:document;XiHan:Bindings:db:value=mongodb://s3cret@db""", "binding 'db' is of type 'connection:document' but the manifest declares 'connection:relational'")]
    public async Task Load_WithABindingThatIsMissingOrOfAnotherType_IsRejected_BeforeLoadingCode(string configured, string reason)
    {
        // 与进程外、Wasm 宿主同一条规则（lifecycle.md §2「bindings 的核对」，ADR-0292）：进程内宿主不下发绑定，但在加载代码之前核对清单声明的
        var directory = CopyWithBindings("""{ "name": "db", "type": "connection:relational" }""");
        try
        {
            await using var host = CreateHost(configured);

            var result = await host.LoadAsync(new PluginSource(directory, _granted), CancellationToken.None);

            Assert.True(result.IsFailure, "A plugin whose required binding is missing or mistyped must not load.");
            Assert.Equal(PluginErrors.LoadFailed, result.Error!.Code);
            Assert.Equal(PluginMessages.LoadFailed, result.Error.MessageKey);
            Assert.Equal("xihan.samples.plugin", result.Error.Details!["id"]);
            Assert.Equal(reason, result.Error.Details["reason"]);
            Assert.DoesNotContain("s3cret", result.Error.ToString(), StringComparison.Ordinal);
            Assert.Empty(host.Plugins);
            // 插件代码一行没跑：程序集没有加载进任何上下文
            Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.Location.StartsWith(directory, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Theory]
    [InlineData("""{ "name": "db", "type": "connection:relational" }""", "XiHan:Bindings:db:host=db.internal")]
    [InlineData("""{ "name": "db", "type": "connection:relational" }""", "XiHan:Bindings:db:type=connection:relational;XiHan:Bindings:db:value=Host=db")]
    [InlineData("""{ "name": "db", "type": "connection:relational", "required": false }""", "")]
    [InlineData("""{ "name": "db", "type": "connection:relational", "required": false }""", "XiHan:Bindings:db:type=queue")]
    public async Task Load_WithBindingsThatAreProvidedOrOptional_Succeeds(string declared, string configured)
    {
        // 只有属性也算给出了；可选的没有内容就不核对类型（不会下发，也就没有拿错类型去连的问题）
        var directory = CopyWithBindings(declared);
        try
        {
            await using var host = CreateHost(configured);

            var result = await host.LoadAsync(new PluginSource(directory, _granted), CancellationToken.None);

            Assert.True(result.IsSuccess, result.ToString());
            Assert.Equal(LifecycleState.Ready, result.ValueOrThrow().State);
            Assert.True((await host.UnloadAsync("xihan.samples.plugin", CancellationToken.None)).IsSuccess);
        }
        finally
        {
            DeleteQuietly(directory);
        }
    }

    [Fact]
    public async Task Load_ChecksTheBindingsUnderTheServiceBindingRootItWasGiven()
    {
        // 绑定只挂在应用指定的根目录下（ADR-0301）：修之前宿主只认环境变量 SERVICE_BINDING_ROOT，核对时找不到这个必需的绑定
        var directory = CopyWithBindings("""{ "name": "db", "type": "connection:relational" }""");
        var root = MountedRoot("postgres://mounted/app");
        try
        {
            await using var given = CreateHost(string.Empty, root);
            var loaded = await given.LoadAsync(new PluginSource(directory, _granted), CancellationToken.None);
            Assert.True(loaded.IsSuccess, loaded.ToString());
            Assert.True((await given.UnloadAsync("xihan.samples.plugin", CancellationToken.None)).IsSuccess);

            // 没给根目录时仍取环境变量：那里没有这个绑定
            await using var defaults = CreateHost(string.Empty);
            var rejected = await defaults.LoadAsync(new PluginSource(directory, _granted), CancellationToken.None);
            Assert.Equal("binding 'db' is required but not provided", rejected.Error!.Details!["reason"]);
        }
        finally
        {
            DeleteQuietly(directory);
            DeleteQuietly(root);
        }
    }

    [Fact]
    public async Task Plugin_ResolvesItsBindings_UnderTheSameRootAsTheHost()
    {
        // 插件经 PluginContext.ServiceBindingRoot 拿到宿主的根目录，自己解析出的与宿主核对的是同一份
        var directory = ProbeDirectory();
        var root = MountedRoot("postgres://mounted/app");
        var slot = "xihan.tests.probe." + Guid.NewGuid().ToString("N");
        try
        {
            await using var host = CreateHost(BindingProbePlugin.SlotKey + "=" + slot, root);

            var loaded = await host.LoadAsync(new PluginSource(directory, PluginPermissions.Empty), CancellationToken.None);

            Assert.True(loaded.IsSuccess, loaded.ToString());
            Assert.Equal(root + "|postgres://mounted/app", AppContext.GetData(slot));
            Assert.True((await host.UnloadAsync(BindingProbePlugin.Id, CancellationToken.None)).IsSuccess);
        }
        finally
        {
            AppContext.SetData(slot, null);
            DeleteQuietly(directory);
            DeleteQuietly(root);
        }
    }

    [Fact]
    public async Task Plugin_WithoutAServiceBindingRoot_SeesNone()
    {
        // 没给根目录时插件拿到 null，交给解析器即取环境变量，与宿主相同
        var directory = ProbeDirectory();
        var slot = "xihan.tests.probe." + Guid.NewGuid().ToString("N");
        try
        {
            await using var host = CreateHost(BindingProbePlugin.SlotKey + "=" + slot + ";XiHan:Bindings:db:value=Host=configured");

            var loaded = await host.LoadAsync(new PluginSource(directory, PluginPermissions.Empty), CancellationToken.None);

            Assert.True(loaded.IsSuccess, loaded.ToString());
            Assert.Equal("|Host=configured", AppContext.GetData(slot));
            Assert.True((await host.UnloadAsync(BindingProbePlugin.Id, CancellationToken.None)).IsSuccess);
        }
        finally
        {
            AppContext.SetData(slot, null);
            DeleteQuietly(directory);
        }
    }

    /// <summary>
    /// 以这些配置项创建宿主
    /// </summary>
    /// <remarks>
    /// <paramref name="values"/> 是以 <c>;</c> 分隔的 <c>键=值</c>，空串表示没有；值里可以再有 <c>=</c>
    /// </remarks>
    /// <param name="values">配置项</param>
    /// <param name="serviceBindingRoot">交给宿主的 Service Binding 根目录；<see langword="null"/> 时宿主取环境变量</param>
    private static InProcessPluginHost CreateHost(string values, string? serviceBindingRoot = null)
    {
        var pairs = values.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(static pair => pair.Split('=', 2)).Select(static pair => new KeyValuePair<string, string?>(pair[0], pair[1]));
        #pragma warning disable IL2026, IL3050 // 测试进程不裁剪、不 AOT
        return new(new ConfigurationBuilder().AddInMemoryCollection(pairs).Build(), NullLoggerFactory.Instance, serviceBindingRoot);
        #pragma warning restore IL2026, IL3050
    }

    /// <summary>
    /// 一个临时的 Service Binding 根目录，里面挂着绑定 <c>db</c>
    /// </summary>
    private static string MountedRoot(string uri)
    {
        var root = Path.Combine(Path.GetTempPath(), "xihan-plugin-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "db"));
        File.WriteAllText(Path.Combine(root, "db", "uri"), uri + "\n");
        return root;
    }

    /// <summary>
    /// <see cref="BindingProbePlugin"/> 的插件目录
    /// </summary>
    /// <remarks>
    /// 本程序集的副本与按它的清单写出的 <c>xihan.manifest.json</c>
    /// </remarks>
    private static string ProbeDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "xihan-plugin-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var assembly = typeof(BindingProbePlugin).Assembly.Location;
        File.Copy(assembly, Path.Combine(directory, Path.GetFileName(assembly)));
        File.WriteAllText(Path.Combine(directory, ManifestReader.FileName), ManifestReader.Write(new BindingProbePlugin().Manifest));
        return directory;
    }

    /// <summary>
    /// 示例插件的副本，清单里加一项绑定声明
    /// </summary>
    private static string CopyWithBindings(string binding)
    {
        var directory = Path.Combine(Path.GetTempPath(), "xihan-plugin-" + Guid.NewGuid().ToString("N"));
        CopyTree(PluginDirectory, directory);
        var manifestPath = Path.Combine(directory, "xihan.manifest.json");
        var json = File.ReadAllText(manifestPath);
        var edited = json.Replace("\"permissions\": [", "\"bindings\": [ " + binding + " ],\n  \"permissions\": [", StringComparison.Ordinal);
        Assert.NotEqual(json, edited);
        File.WriteAllText(manifestPath, edited);
        return directory;
    }

    /// <summary>
    /// 删除临时目录，删不掉就留给系统清理
    /// </summary>
    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 插件程序集已被加载时文件仍被占用：临时目录留给系统清理，不掩盖断言本身
        }
    }

    /// <summary>
    /// 递归复制目录
    /// </summary>
    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var child in Directory.EnumerateDirectories(source))
        {
            CopyTree(child, Path.Combine(target, Path.GetFileName(child)));
        }
    }
}
