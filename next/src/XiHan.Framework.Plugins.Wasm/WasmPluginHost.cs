// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wasmtime;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Plugins.Wasm.Instances;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// Wasm 插件宿主（<c>isolation: wasm</c>，RFC-0018，Experimental）：按目录发现插件、编译并握手、把声明的操作与订阅接进宿主、健康轮询与崩溃重建，并把每个插件作为健康条目上报。
/// 通常由 <see cref="WasmPluginsFeature"/> 装配；也可按 <see cref="IPluginHost"/> 在运行期加载与卸载（贡献中间件的插件除外）。
/// </summary>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed partial class WasmPluginHost : IPluginHost, IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 健康条目名前缀。
    /// </summary>
    public const string HealthPrefix = "plugins.";

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly WasmPluginOptions _options;

    /// <summary>
    /// 已登记的运行时，按清单 ID。
    /// </summary>
    private readonly ConcurrentDictionary<string, WasmPluginRuntime> _plugins = new(StringComparer.Ordinal);

    /// <summary>
    /// 登记顺序（停止按相反顺序）。
    /// </summary>
    private readonly List<WasmPluginRuntime> _order = [];

    /// <summary>
    /// 串行化加载与卸载。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 插件共享的宿主环境。
    /// </summary>
    private readonly WasmHostContext _context;

    /// <summary>
    /// epoch 计时器：按间隔递增引擎 epoch，让各实例的截止时间生效。
    /// </summary>
    private readonly CancellationTokenSource _ticker = new();

    /// <summary>
    /// 计时循环；释放时先等它退出，引擎才能安全释放。
    /// </summary>
    private readonly Task _ticking;

    /// <summary>
    /// 已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 日志工厂；附着到容器后换成宿主的。
    /// </summary>
    private ILoggerFactory _loggerFactory;

    /// <summary>
    /// 日志。
    /// </summary>
    private ILogger _logger;

    /// <summary>
    /// 代理操作登记到的登记表；由特性在配置阶段提供。
    /// </summary>
    private OperationRegistry? _registry;

    /// <summary>
    /// 创建宿主：建一个启用 epoch 中断的引擎并开始计时。
    /// </summary>
    /// <param name="options">选项。</param>
    /// <param name="configuration">宿主配置。</param>
    /// <param name="clock">时钟。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    /// <param name="defaultLocale">宿主默认区域。</param>
    /// <exception cref="FrameworkException">宿主进程强制用户态影子栈（CET），Wasm 陷阱会终止进程；宿主可执行项目须设置 <c>CETCompat=false</c>。</exception>
    public WasmPluginHost(WasmPluginOptions options, IConfiguration configuration, TimeProvider clock, ILoggerFactory loggerFactory, string defaultLocale = "en-US")
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLocale);
        _logger = loggerFactory.CreateLogger<WasmPluginHost>();
        // 影子栈强制时第一次陷阱就会让进程 fail-fast，建引擎前就拒绝
        ShadowStackGuard.EnsureTrapsAreSafe();
        var engine = new Engine(new Config().WithEpochInterruption(true).WithMaximumStackSize(options.StackLimitBytes));
        _context = new WasmHostContext(
            engine,
            options,
            configuration,
            clock,
            int.Parse(FrameworkInfo.SpecVersion, System.Globalization.CultureInfo.InvariantCulture),
            defaultLocale,
            Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty,
            options.ResolveWorkDirectory());
        _ticking = TickAsync(engine, options.EpochInterval, _ticker.Token);
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginStatus> Plugins => _plugins.Values.Select(static r => r.Status).OrderBy(static s => s.Manifest.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 一个插件的诊断快照；未加载为空。
    /// </summary>
    public WasmPluginStatus? Describe(string manifestId)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        return _plugins.TryGetValue(manifestId, out var runtime) ? runtime.Describe() : null;
    }

    /// <summary>
    /// 全部运行时（特性登记代理操作、订阅与中间件时使用）。
    /// </summary>
    internal IReadOnlyList<WasmPluginRuntime> Runtimes => _order.ToList();

    /// <summary>
    /// 指定代理操作的登记表。
    /// </summary>
    internal void UseRegistry(OperationRegistry registry) => _registry = registry;

    /// <summary>
    /// 同步发现一个插件目录（配置阶段使用）：读清单、按选项授予权限与映射、编译并握手。失败的插件仍登记在宿主里以 Failed 上报。
    /// </summary>
    internal Result<WasmPluginRuntime> Discover(string directory)
        => DiscoverAsync(directory, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<Result<PluginStatus>> LoadAsync(PluginSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var manifest = ReadManifest(source.Directory);
        if (!manifest.TryGetValue(out var read))
        {
            return manifest.Error!;
        }

        // 中间件必须在管道构建前登记：运行期加载的插件不能贡献中间件
        if (read.Contributes.Pipeline.Count > 0)
        {
            return new Error(PluginErrors.InvalidModule, PluginMessages.ModuleInvalid, MessageArguments.Of(("id", read.Id), ("reason", "plugins contributing middleware cannot be loaded at run time")));
        }

        var discovered = await DiscoverAsync(source, read, _options.MountsFor(read.Id), cancellationToken).ConfigureAwait(false);
        if (!discovered.TryGetValue(out var runtime))
        {
            return discovered.Error!;
        }

        var registered = Register(runtime);
        if (registered.IsFailure)
        {
            await runtime.StopAsync("unload", cancellationToken).ConfigureAwait(false);
            Remove(runtime);
            return registered.Error!;
        }

        var started = await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
        return started.IsSuccess ? runtime.Status : started.Error!;
    }

    /// <inheritdoc />
    public async Task<Result> UnloadAsync(string manifestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        WasmPluginRuntime? runtime;
        try
        {
            if (!_plugins.TryRemove(manifestId, out runtime))
            {
                return Error.NotFound(PluginErrors.NotFound, PluginMessages.NotLoaded, MessageArguments.Of(("id", manifestId)));
            }

            _order.Remove(runtime);
        }
        finally
        {
            _gate.Release();
        }

        await runtime.StopAsync("unload", cancellationToken).ConfigureAwait(false);
        runtime.Dispose();
        return Result.Success();
    }

    /// <summary>
    /// 附着到已构建的宿主容器：换用宿主日志、开放回调、启动 <c>onStartup</c> 的插件（失败只记日志）。
    /// </summary>
    internal async Task AttachAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _loggerFactory = services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<WasmPluginHost>();
        foreach (var runtime in _order)
        {
            runtime.Logger = _loggerFactory.CreateLogger<WasmPluginRuntime>();
        }

        _context.Services = services;
        foreach (var runtime in _order.Where(static r => r.Handshake is not null && !r.IsLazy))
        {
            var started = await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
            if (started.IsFailure)
            {
                LogStartFailed(_logger, runtime.Manifest.Id, started.Error!.Code, started.Error.Message);
            }
        }
    }

    /// <summary>
    /// 停止全部插件（与登记相反的顺序）。
    /// </summary>
    internal async Task StopAllAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in Enumerable.Reverse(_order.ToList()))
        {
            await runtime.StopAsync("shutdown", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 把一条消息投递给订阅了该主题与类型的插件；第一个失败即为结果。
    /// </summary>
    internal async ValueTask<Result> DeliverAsync(MessageDelivery delivery, InvocationContext context)
    {
        foreach (var runtime in _order.Where(r => r.IsSubscribedTo(delivery.Topic, delivery.Envelope.Type)))
        {
            var delivered = await runtime.DeliverAsync(delivery, context).ConfigureAwait(false);
            if (delivered.IsFailure)
            {
                return delivered;
            }
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
        => _order.Select(static r => new ContributorStatus(HealthPrefix + r.Manifest.Id, r.State, r.Error)).ToList();

    /// <summary>
    /// 停止全部插件、停止 epoch 计时并释放引擎。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAllAsync(CancellationToken.None).ConfigureAwait(false);
        foreach (var runtime in _order.ToList())
        {
            runtime.Dispose();
        }

        // 计时器可能正在调用引擎：先停它再释放引擎
        await _ticker.CancelAsync().ConfigureAwait(false);
        await _ticking.ConfigureAwait(false);
        _ticker.Dispose();
        _context.Engine.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// 按间隔递增 epoch。
    /// </summary>
    private static async Task TickAsync(Engine engine, TimeSpan interval, CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                engine.IncrementEpoch();
            }
        }
        catch (OperationCanceledException)
        {
            // 宿主释放
        }
        catch (ObjectDisposedException)
        {
            // 引擎已释放
        }
    }

    /// <summary>
    /// 按目录发现：读清单、按选项取授予与映射。
    /// </summary>
    private async Task<Result<WasmPluginRuntime>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(directory);
        var manifest = ReadManifest(full);
        if (!manifest.TryGetValue(out var read))
        {
            return manifest.Error!;
        }

        return await DiscoverAsync(new PluginSource(full, _options.GrantsFor(read.Id)), read, _options.MountsFor(read.Id), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 校验权限（<c>capability:</c> 须授予、<c>fs.*</c> 须映射、<c>net.outbound:</c> 永不授予）与模块文件，登记运行时并探测启动。
    /// </summary>
    private async Task<Result<WasmPluginRuntime>> DiscoverAsync(PluginSource source, Manifest manifest, IReadOnlyDictionary<string, string> mounts, CancellationToken cancellationToken)
    {
        var missing = manifest.Permissions.Where(permission => !IsGranted(permission, source.Permissions, mounts)).ToList();
        if (missing.Count > 0)
        {
            return Error.Denied(PluginErrors.PermissionDenied, PluginMessages.PermissionsNotGranted, MessageArguments.Of(("id", manifest.Id), ("permissions", string.Join(", ", missing))));
        }

        if (string.IsNullOrWhiteSpace(manifest.Entry))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.EntryRequired, MessageArguments.Of(("id", manifest.Id)));
        }

        var modulePath = Path.GetFullPath(Path.Combine(source.Directory, manifest.Entry));
        if (!File.Exists(modulePath))
        {
            return Error.NotFound(PluginErrors.NotFound, PluginMessages.ExecutableMissing, MessageArguments.Of(("path", modulePath)));
        }

        WasmPluginRuntime runtime;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_plugins.ContainsKey(manifest.Id))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, PluginMessages.AlreadyLoaded, MessageArguments.Of(("id", manifest.Id)));
            }

            runtime = new WasmPluginRuntime(manifest, source.Directory, modulePath, source.Permissions, mounts, _context, _loggerFactory.CreateLogger<WasmPluginRuntime>());
            _plugins[manifest.Id] = runtime;
            _order.Add(runtime);
        }
        finally
        {
            _gate.Release();
        }

        var discovered = await runtime.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        return discovered.IsSuccess ? runtime : discovered.Error!;
    }

    /// <summary>
    /// 一项清单权限是否被授予：<c>capability:</c> 看宿主授予，<c>fs.read:</c> / <c>fs.write:</c> 看是否映射，其余（含 <c>net.outbound:</c>）不授予。
    /// </summary>
    private static bool IsGranted(string permission, PermissionGrant granted, IReadOnlyDictionary<string, string> mounts)
    {
        if (permission.StartsWith("capability:", StringComparison.Ordinal))
        {
            return granted.IsGranted(permission);
        }

        if (permission.StartsWith("fs.read:", StringComparison.Ordinal))
        {
            return mounts.ContainsKey(permission["fs.read:".Length..]);
        }

        return permission.StartsWith("fs.write:", StringComparison.Ordinal) && mounts.ContainsKey(permission["fs.write:".Length..]);
    }

    /// <summary>
    /// 把运行时握手声明的操作登记为代理操作。
    /// </summary>
    private Result Register(WasmPluginRuntime runtime)
    {
        if (_registry is null || runtime.Handshake is null)
        {
            return Result.Success();
        }

        foreach (var operation in runtime.Handshake.Operations)
        {
            var added = _registry.Add(new ProxyOperationDescriptor(operation.Contract, runtime.Manifest.Id, runtime, operation.Schema));
            if (added.IsFailure)
            {
                return added;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 从登记里移除。
    /// </summary>
    private void Remove(WasmPluginRuntime runtime)
    {
        _plugins.TryRemove(runtime.Manifest.Id, out _);
        lock (_order)
        {
            _order.Remove(runtime);
        }
    }

    /// <summary>
    /// 读目录里的清单并确认是 Wasm 插件。
    /// </summary>
    private static Result<Manifest> ReadManifest(string directory)
    {
        var read = ManifestReader.ReadFile(Path.Combine(directory, ManifestReader.FileName));
        if (!read.TryGetValue(out var manifest))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.ManifestInvalid, MessageArguments.Of(("directory", directory), ("reason", read.Error!.Message)), read.Error);
        }

        if (manifest.Kind != ManifestKind.Plugin || manifest.Isolation != PluginIsolation.Wasm)
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.NotOutOfProcess, MessageArguments.Of(("id", manifest.Id), ("kind", manifest.Kind.ToString()), ("isolation", manifest.Isolation?.ToString())));
        }

        return manifest;
    }

    /// <summary>
    /// 记录插件启动失败（宿主继续运行）。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Wasm plugin {PluginId} failed to start: {Code} {Message}")]
    private static partial void LogStartFailed(ILogger logger, string pluginId, string code, string message);
}
