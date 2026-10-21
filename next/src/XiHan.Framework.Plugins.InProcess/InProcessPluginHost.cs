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
    /// <summary>
    /// 宿主配置；以只读视图传给每个插件的 <see cref="PluginContext"/>。
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 宿主日志。
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 已加载插件，按清单 ID 索引；读写都在 <see cref="_gate"/> 内进行。
    /// </summary>
    private readonly Dictionary<string, LoadedPlugin> _plugins = new(StringComparer.Ordinal);

    /// <summary>
    /// 串行化加载、卸载与释放的互斥量，保证同一清单 ID 不会被并发加载两次。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 创建插件宿主。进程内插件按清单中的类型名实例化，需要运行期反射与动态代码。
    /// </summary>
    /// <param name="configuration">宿主配置（只读视图传给插件）。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    [RequiresUnreferencedCode("In-process plugins load assemblies and instantiate entry types by the names in the manifest; trimmed types may not be found.")]
    [RequiresDynamicCode("In-process plugins instantiate entry types by the names in the manifest, which requires runtime reflection.")]
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
            // 同步等待互斥量：属性访问不能异步，且临界区只是复制一份状态快照
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

        // 清单、权限与程序集文件的校验不涉及共享状态，放在互斥量之外完成，缩短临界区
        var manifestPath = Path.Combine(source.Directory, ManifestReader.FileName);
        var read = ManifestReader.ReadFile(manifestPath);
        if (!read.TryGetValue(out var manifest))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.ManifestInvalid, MessageArguments.Of(("directory", source.Directory), ("reason", read.Error!.Message)), read.Error);
        }

        if (manifest.Kind != ManifestKind.Plugin || manifest.Isolation != PluginIsolation.InProcess)
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.NotInProcess, MessageArguments.Of(("id", manifest.Id), ("kind", manifest.Kind.ToString()), ("isolation", manifest.Isolation?.ToString())));
        }

        // 权限在加载任何代码之前判定：清单声明了而宿主未授予的权限，一项也不能少
        var missing = source.Permissions.Missing(manifest);
        if (missing.Count > 0)
        {
            return Error.Denied(PluginErrors.PermissionDenied, PluginMessages.PermissionsNotGranted, MessageArguments.Of(("id", manifest.Id), ("permissions", string.Join(", ", missing))));
        }

        if (string.IsNullOrWhiteSpace(manifest.Package) || string.IsNullOrWhiteSpace(manifest.Entry))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.EntryRequired, MessageArguments.Of(("id", manifest.Id)));
        }

        var assemblyPath = Path.Combine(source.Directory, manifest.Package + ".dll");
        if (!File.Exists(assemblyPath))
        {
            return Error.NotFound(PluginErrors.NotFound, PluginMessages.AssemblyMissing, MessageArguments.Of(("path", assemblyPath)));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 重复检查必须在互斥量内，否则两个并发加载可能都通过检查
            if (_plugins.ContainsKey(manifest.Id))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, PluginMessages.AlreadyLoaded, MessageArguments.Of(("id", manifest.Id)));
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
            // 先从表里移除再停止：停止期间即使有人查询，也不会再看到这个插件
            if (!_plugins.Remove(manifestId, out var plugin))
            {
                return Error.NotFound(PluginErrors.NotFound, PluginMessages.NotLoaded, MessageArguments.Of(("id", manifestId)));
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
            // 逐个停止，单个插件停止失败只记在它自己的状态里，不中断其余插件的释放
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

    /// <summary>
    /// 在独立加载上下文里加载程序集、实例化入口类型、配置并启动插件。
    /// 任一步失败都释放已建的容器并卸载上下文，以 <see cref="Result{T}"/> 返回错误；取消异常原样向上传播。
    /// </summary>
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
                return Error.NotFound(PluginErrors.NotFound, PluginMessages.EntryNotFound, MessageArguments.Of(("entry", manifest.Entry), ("package", manifest.Package)));
            }

            if (Activator.CreateInstance(entryType) is not IPlugin plugin)
            {
                context.Unload();
                return new Error(PluginErrors.InvalidEntry, PluginMessages.EntryNotPlugin, MessageArguments.Of(("entry", manifest.Entry)));
            }

            // 以文件清单为准：入口类型自带的清单 ID 必须与之一致，防止程序集被放进错误的目录
            if (!string.Equals(plugin.Manifest.Id, manifest.Id, StringComparison.Ordinal))
            {
                context.Unload();
                return new Error(PluginErrors.InvalidEntry, PluginMessages.EntryManifestMismatch, MessageArguments.Of(("actual", plugin.Manifest.Id), ("expected", manifest.Id)));
            }

            // 插件拥有独立的服务容器，不能解析宿主服务；管道暂不开放给插件
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
            return Error.Unexpected(PluginErrors.LoadFailed, PluginMessages.LoadFailed, exception is FrameworkException framework ? framework.ToError() : null, MessageArguments.Of(("id", manifest.Id), ("reason", exception.Message)));
        }
    }

    /// <summary>
    /// 记录插件进入某个生命周期状态。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin {PluginId} entered {State}.")]
    private static partial void LogPluginState(ILogger logger, string pluginId, LifecycleState state);

    /// <summary>
    /// 记录插件加载或启动期间的未处理异常。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} failed.")]
    private static partial void LogPluginFailed(ILogger logger, Exception exception, string pluginId);

    /// <summary>
    /// 一个已加载插件的运行时句柄：实例、专属服务容器与加载上下文，以及当前状态。
    /// </summary>
    private sealed class LoadedPlugin(Manifest manifest, IPlugin plugin, ServiceProvider provider, PluginLoadContext context)
    {
        /// <summary>
        /// 当前生命周期状态；加载成功即为 <see cref="LifecycleState.Ready"/>。
        /// </summary>
        public LifecycleState State { get; private set; } = LifecycleState.Ready;

        /// <summary>
        /// 停止失败时的错误；正常情况下为 <c>null</c>。
        /// </summary>
        public Error? Error { get; private set; }

        /// <summary>
        /// 对外暴露的状态快照。
        /// </summary>
        public PluginStatus Status => new(manifest, State, Error);

        /// <summary>
        /// 停止插件并释放其容器与加载上下文。停止抛出的异常记为 <see cref="LifecycleState.Failed"/>，资源仍然释放。
        /// </summary>
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
                Error = Error.Unexpected(PluginErrors.LoadFailed, PluginMessages.StopFailed, details: MessageArguments.Of(("id", manifest.Id), ("reason", exception.Message)));
                State = LifecycleState.Failed;
            }
            finally
            {
                // 无论停止成功与否都释放容器并卸载上下文，可收集上下文随之允许被 GC 回收
                await provider.DisposeAsync().ConfigureAwait(false);
                context.Unload();
            }
        }
    }
}
