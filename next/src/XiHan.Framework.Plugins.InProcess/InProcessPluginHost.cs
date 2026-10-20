// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.InProcess;

/// <summary>
/// 进程内插件宿主。每个插件拥有自己的可收集加载上下文与服务容器；失败以 <see cref="Result"/> 返回，不影响宿主。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class InProcessPluginHost : IPluginHost, IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly Dictionary<string, LoadedPlugin> _plugins = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 创建插件宿主。进程内插件按清单中的类型名实例化，需要运行期反射与动态代码。
    /// </summary>
    /// <param name="configuration">宿主配置（只读视图传给插件）。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    [RequiresUnreferencedCode("进程内插件按清单中的类型名加载程序集并实例化，裁剪后可能找不到类型。")]
    [RequiresDynamicCode("进程内插件按清单中的类型名实例化，需要运行期反射。")]
    public InProcessPluginHost(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<InProcessPluginHost>();
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginStatus> Plugins
    {
        get
        {
            _gate.Wait();
            try
            {
                return _plugins.Values.Select(static p => p.Status).ToList();
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "构造函数已声明 RequiresUnreferencedCode。")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "构造函数已声明 RequiresDynamicCode。")]
    public async Task<Result<PluginStatus>> LoadAsync(PluginSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var manifestPath = Path.Combine(source.Directory, ManifestReader.FileName);
        var read = ManifestReader.ReadFile(manifestPath);
        if (!read.TryGetValue(out var manifest))
        {
            return new Error(PluginErrors.InvalidSource, $"插件目录 '{source.Directory}' 的清单无效：{read.Error!.Message}");
        }

        if (manifest.Kind != ManifestKind.Plugin || manifest.Isolation != PluginIsolation.InProcess)
        {
            return new Error(PluginErrors.InvalidSource, $"清单 '{manifest.Id}' 不是 in-process 插件（kind={manifest.Kind}，isolation={manifest.Isolation}）。");
        }

        var missing = source.Permissions.Missing(manifest);
        if (missing.Count > 0)
        {
            return Error.Denied(PluginErrors.PermissionDenied, $"插件 '{manifest.Id}' 声明的权限未授予：{string.Join("、", missing)}。");
        }

        if (string.IsNullOrWhiteSpace(manifest.Package) || string.IsNullOrWhiteSpace(manifest.Entry))
        {
            return new Error(PluginErrors.InvalidSource, $"插件清单 '{manifest.Id}' 必须声明 package（程序集名）与 entry（入口类型）。");
        }

        var assemblyPath = Path.Combine(source.Directory, manifest.Package + ".dll");
        if (!File.Exists(assemblyPath))
        {
            return Error.NotFound(PluginErrors.NotFound, $"插件程序集 '{assemblyPath}' 不存在。");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_plugins.ContainsKey(manifest.Id))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, $"插件 '{manifest.Id}' 已加载。");
            }

            var loaded = await ActivateAsync(manifest, source, assemblyPath, cancellationToken).ConfigureAwait(false);
            if (!loaded.TryGetValue(out var plugin))
            {
                return loaded.Error!;
            }

            _plugins[manifest.Id] = plugin;
            LogPluginState(_logger, manifest.Id, plugin.State);
            return plugin.Status;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result> UnloadAsync(string manifestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_plugins.Remove(manifestId, out var plugin))
            {
                return Error.NotFound(PluginErrors.NotFound, $"插件 '{manifestId}' 未加载。");
            }

            await plugin.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            LogPluginState(_logger, manifestId, plugin.State);
            return Result.Success();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 停止并卸载全部插件。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var plugin in _plugins.Values.ToList())
            {
                await plugin.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
            }

            _plugins.Clear();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "构造函数已声明 RequiresUnreferencedCode。")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "构造函数已声明 RequiresUnreferencedCode。")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "构造函数已声明 RequiresDynamicCode。")]
    private async Task<Result<LoadedPlugin>> ActivateAsync(Manifest manifest, PluginSource source, string assemblyPath, CancellationToken cancellationToken)
    {
        var context = new PluginLoadContext(manifest.Id, assemblyPath);
        ServiceProvider? provider = null;
        try
        {
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var entryType = assembly.GetType(manifest.Entry!, throwOnError: false);
            if (entryType is null)
            {
                context.Unload();
                return Error.NotFound(PluginErrors.NotFound, $"插件程序集 '{manifest.Package}' 中没有入口类型 '{manifest.Entry}'。");
            }

            if (Activator.CreateInstance(entryType) is not IPlugin plugin)
            {
                context.Unload();
                return new Error(PluginErrors.InvalidEntry, $"入口类型 '{manifest.Entry}' 没有实现 IPlugin。");
            }

            if (!string.Equals(plugin.Manifest.Id, manifest.Id, StringComparison.Ordinal))
            {
                context.Unload();
                return new Error(PluginErrors.InvalidEntry, $"入口类型的清单 ID '{plugin.Manifest.Id}' 与清单文件 '{manifest.Id}' 不一致。");
            }

            var services = new ServiceCollection();
            plugin.Configure(new PluginContext(manifest, services, _configuration, source.Permissions, pipeline: null));
            provider = services.BuildServiceProvider();

            await plugin.StartAsync(provider, cancellationToken).ConfigureAwait(false);
            return new LoadedPlugin(manifest, plugin, provider, context);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPluginFailed(_logger, exception, manifest.Id);
            if (provider is not null)
            {
                await provider.DisposeAsync().ConfigureAwait(false);
            }

            context.Unload();
            return Error.Unexpected(PluginErrors.LoadFailed, $"插件 '{manifest.Id}' 加载失败：{exception.Message}", exception is FrameworkException framework ? framework.ToError() : null);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "插件 {PluginId} 进入 {State}")]
    private static partial void LogPluginState(ILogger logger, string pluginId, LifecycleState state);

    [LoggerMessage(Level = LogLevel.Error, Message = "插件 {PluginId} 失败")]
    private static partial void LogPluginFailed(ILogger logger, Exception exception, string pluginId);

    private sealed class LoadedPlugin(Manifest manifest, IPlugin plugin, ServiceProvider provider, PluginLoadContext context)
    {
        public LifecycleState State { get; private set; } = LifecycleState.Ready;

        public Error? Error { get; private set; }

        public PluginStatus Status => new(manifest, State, Error);

        public async Task ShutdownAsync(CancellationToken cancellationToken)
        {
            State = LifecycleState.Draining;
            try
            {
                await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
                State = LifecycleState.Stopped;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Error = Error.Unexpected(PluginErrors.LoadFailed, $"插件 '{manifest.Id}' 停止失败：{exception.Message}");
                State = LifecycleState.Failed;
            }
            finally
            {
                await provider.DisposeAsync().ConfigureAwait(false);
                context.Unload();
            }
        }
    }
}
