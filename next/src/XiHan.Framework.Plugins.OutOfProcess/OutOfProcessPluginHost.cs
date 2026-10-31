// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Capabilities;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.OutOfProcess.Callbacks;
using XiHan.Framework.Plugins.OutOfProcess.Runtime;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 进程外插件宿主（<c>isolation: out-of-process</c>，RFC-0017）。
/// </summary>
/// <remarks>
/// 按目录发现插件、启动并握手、把声明的操作与订阅接进宿主、监视健康与崩溃重启、运行宿主回调服务，并把每个插件作为健康条目上报。通常由 <see cref="OutOfProcessPluginsFeature"/> 装配；也可按 <see cref="IPluginHost"/> 在运行期加载与卸载。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class OutOfProcessPluginHost : IPluginHost, IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 回调 <c>Host.Invoke</c> 需要的权限。
    /// </summary>
    public const string PipelinePermission = PluginCallbackPermissions.Pipeline;

    /// <summary>
    /// 回调 <c>Host.Publish</c> 需要的权限。
    /// </summary>
    public const string MessagingPermission = PluginCallbackPermissions.Messaging;

    /// <summary>
    /// 回调时宿主所用主体的认证方案。
    /// </summary>
    public const string PluginScheme = "Plugin";

    /// <summary>
    /// 健康条目名前缀。
    /// </summary>
    public const string HealthPrefix = "plugins.";

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly OutOfProcessPluginOptions _options;

    /// <summary>
    /// 会话登记表。
    /// </summary>
    private readonly PluginSessions _sessions = new();

    /// <summary>
    /// 已登记的运行时，按清单 ID。
    /// </summary>
    private readonly ConcurrentDictionary<string, PluginRuntime> _plugins = new(StringComparer.Ordinal);

    /// <summary>
    /// 登记顺序（停止按相反顺序）。
    /// </summary>
    private readonly List<PluginRuntime> _order = [];

    /// <summary>
    /// 清单读不出来的插件目录与原因。
    /// </summary>
    /// <remarks>
    /// 没有清单 ID，只作为健康条目 <c>plugins.&lt;目录名&gt;</c> 上报；只在配置阶段写入。
    /// </remarks>
    private readonly List<(string Directory, Error Error)> _unreadable = [];

    /// <summary>
    /// 串行化加载与卸载。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 插件共享的宿主环境。
    /// </summary>
    private readonly PluginHostContext _context;

    /// <summary>
    /// 日志工厂。
    /// </summary>
    /// <remarks>
    /// 附着到容器后换成宿主的。
    /// </remarks>
    private ILoggerFactory _loggerFactory;

    /// <summary>
    /// 日志。
    /// </summary>
    private ILogger _logger;

    /// <summary>
    /// 代理操作登记到的登记表。
    /// </summary>
    /// <remarks>
    /// 由特性在配置阶段提供。
    /// </remarks>
    private OperationRegistry? _registry;

    /// <summary>
    /// 回调服务器。
    /// </summary>
    /// <remarks>
    /// 附着后启动。
    /// </remarks>
    private PluginCallbackServer? _server;

    /// <summary>
    /// 创建宿主。
    /// </summary>
    /// <remarks>
    /// 回调端点在此刻选定（启动插件时经环境变量下发），服务器在 <see cref="AttachAsync"/> 时才监听。
    /// </remarks>
    /// <param name="options">选项。</param>
    /// <param name="configuration">宿主配置（插件配置节与绑定从中读取）。</param>
    /// <param name="clock">时钟。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    /// <param name="stopGrace">停止宽限（通常取内核的停止超时）。</param>
    /// <param name="defaultLocale">宿主默认区域。</param>
    public OutOfProcessPluginHost(OutOfProcessPluginOptions options, IConfiguration configuration, TimeProvider clock, ILoggerFactory loggerFactory, TimeSpan stopGrace, string defaultLocale = "en-US")
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLocale);
        _logger = loggerFactory.CreateLogger<OutOfProcessPluginHost>();
        HostEndpoint = PluginCallbackServer.CreateEndpoint();
        _context = new PluginHostContext(
            HostEndpoint,
            int.Parse(FrameworkInfo.SpecVersion, System.Globalization.CultureInfo.InvariantCulture),
            defaultLocale,
            Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty,
            options.ResolveWorkDirectory(),
            options,
            configuration,
            clock,
            _sessions,
            options.StopGrace ?? stopGrace);
    }

    /// <summary>
    /// 宿主回调端点。
    /// </summary>
    public PluginEndpoint HostEndpoint { get; }

    /// <inheritdoc />
    public IReadOnlyList<PluginStatus> Plugins => _plugins.Values.Select(static r => r.Status).OrderBy(static s => s.Manifest.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 一个插件的诊断快照。
    /// </summary>
    /// <remarks>
    /// 未加载为空。
    /// </remarks>
    public OutOfProcessPluginStatus? Describe(string manifestId)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        return _plugins.TryGetValue(manifestId, out var runtime) ? runtime.Describe() : null;
    }

    /// <summary>
    /// 全部运行时（特性登记代理操作与订阅时使用）。
    /// </summary>
    internal IReadOnlyList<PluginRuntime> Runtimes => _order.ToList();

    /// <summary>
    /// 指定代理操作的登记表。
    /// </summary>
    internal void UseRegistry(OperationRegistry registry) => _registry = registry;

    /// <summary>
    /// 同步发现一个插件目录（配置阶段使用）。
    /// </summary>
    /// <remarks>
    /// 读清单、按选项授予权限、启动并握手。失败的插件仍登记在宿主里以 Failed 上报：发现时被拒的（清单、权限、入口不合格）可描述、是健康条目，清单读不出来的只作为健康条目 <c>plugins.&lt;目录名&gt;</c>（<c>lifecycle.md</c> §1）；附着到容器时逐个记日志。
    /// </remarks>
    internal Result<PluginRuntime> Discover(string directory)
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

        var discovered = await DiscoverAsync(source, read, atStartup: false, cancellationToken).ConfigureAwait(false);
        if (!discovered.TryGetValue(out var runtime))
        {
            return discovered.Error!;
        }

        // 运行期加载的代理操作只对内核调用器可见：协议适配器在启动时已经发布过路由
        var registered = Register(runtime);
        if (registered.IsFailure)
        {
            await runtime.StopAsync("unload", cancellationToken).ConfigureAwait(false);
            Remove(runtime);
            return registered.Error!;
        }

        var started = await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
        if (started.IsFailure)
        {
            // 失败返回给调用方，插件保持 Failed 并按重启策略重试，直到卸载（lifecycle.md §6）
            runtime.ScheduleRestart();
            return started.Error!;
        }

        return runtime.Status;
    }

    /// <inheritdoc />
    public async Task<Result> UnloadAsync(string manifestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        PluginRuntime? runtime;
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
    /// 附着到已构建的宿主容器。
    /// </summary>
    /// <remarks>
    /// 换用宿主日志与时钟、启动回调服务器、记录发现阶段失败的插件、启动 <c>onStartup</c> 的插件。握手失败与启动失败的插件按重启策略在后台重试（<c>lifecycle.md</c> §6），发现时被拒的不重试。
    /// </remarks>
    internal async Task AttachAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _context.Clock = services.GetService<TimeProvider>() ?? _context.Clock;
        _loggerFactory = services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<OutOfProcessPluginHost>();
        foreach (var runtime in _order)
        {
            runtime.Logger = _loggerFactory.CreateLogger<PluginRuntime>();
        }

        _server = new PluginCallbackServer(HostEndpoint, _sessions, services, _loggerFactory);
        await _server.StartAsync(cancellationToken).ConfigureAwait(false);
        LogServer(_logger, HostEndpoint);

        // 配置阶段还没有宿主日志：清单读不出来、发现时被拒与握手失败的插件在这里补记
        foreach (var (directory, error) in _unreadable)
        {
            LogDiscoveryFailed(_logger, directory, error.Code, error.Message);
        }

        foreach (var runtime in _order.ToList())
        {
            if (runtime.Handshake is null)
            {
                LogDiscoveryFailed(_logger, runtime.Manifest.Id, runtime.Error?.Code ?? string.Empty, runtime.Error?.Message ?? string.Empty);
                if (!runtime.IsRejected)
                {
                    RetryDiscovery(runtime);
                }

                continue;
            }

            if (runtime.IsLazy)
            {
                continue;
            }

            var started = await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
            if (started.IsFailure)
            {
                LogStartFailed(_logger, runtime.Manifest.Id, started.Error!.Code, started.Error.Message);
                runtime.ScheduleRestart();
            }
        }
    }

    /// <summary>
    /// 停止全部插件（与登记相反的顺序）并关闭回调服务器。
    /// </summary>
    internal async Task StopAllAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in Enumerable.Reverse(_order.ToList()))
        {
            await runtime.StopAsync("shutdown", cancellationToken).ConfigureAwait(false);
        }

        if (_server is { } server)
        {
            _server = null;
            await server.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 把一条消息投递给订阅了该主题与类型的插件。
    /// </summary>
    /// <remarks>
    /// 第一个失败即为结果。
    /// </remarks>
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
        => _order.Select(static r => new ContributorStatus(HealthPrefix + r.Manifest.Id, r.State, r.Error))
            .Concat(_unreadable.Select(static u => new ContributorStatus(HealthPrefix + Path.GetFileName(u.Directory), LifecycleState.Failed, u.Error)))
            .ToList();

    /// <summary>
    /// 已释放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 停止全部插件并释放。
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

        _gate.Dispose();
    }

    /// <summary>
    /// 按目录发现：读清单、按选项取授予。
    /// </summary>
    private async Task<Result<PluginRuntime>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var file = ReadManifestFile(full);
        if (!file.TryGetValue(out var read))
        {
            // 没有清单 ID：只能按目录名上报
            _unreadable.Add((full, file.Error!));
            return file.Error!;
        }

        var source = new PluginSource(full, _options.GrantsFor(read.Id));
        var kind = CheckKind(read);
        if (kind.IsFailure)
        {
            return await RejectAsync(source, read, kind.Error!, cancellationToken).ConfigureAwait(false);
        }

        return await DiscoverAsync(source, read, atStartup: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 校验权限与可执行文件，登记运行时并探测启动。
    /// </summary>
    /// <remarks>
    /// <paramref name="atStartup"/> 为真（配置阶段按目录发现）时被拒的插件以 Failed 登记、握手失败的重试留到附着到容器之后；为假（运行期加载）时被拒只把错误返回给调用方，握手失败的插件保持 Failed 并立即按重启策略重试（<c>lifecycle.md</c> §6）。
    /// </remarks>
    private async Task<Result<PluginRuntime>> DiscoverAsync(PluginSource source, Manifest manifest, bool atStartup, CancellationToken cancellationToken)
    {
        // 权限在启动任何代码之前判定：清单声明了而宿主未授予的权限，一项也不能少
        var rejected = Preflight(source, manifest, out var executable);
        if (rejected is not null)
        {
            return atStartup ? await RejectAsync(source, manifest, rejected, cancellationToken).ConfigureAwait(false) : rejected;
        }

        PluginRuntime runtime;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_plugins.ContainsKey(manifest.Id))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, PluginMessages.AlreadyLoaded, MessageArguments.Of(("id", manifest.Id)));
            }

            runtime = new PluginRuntime(manifest, source.Directory, executable!, source.Permissions, _context, _loggerFactory.CreateLogger<PluginRuntime>());
            _plugins[manifest.Id] = runtime;
            _order.Add(runtime);
        }
        finally
        {
            _gate.Release();
        }

        var discovered = await runtime.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        if (discovered.IsFailure && !atStartup)
        {
            RetryDiscovery(runtime);
        }

        return discovered.IsSuccess ? runtime : discovered.Error!;
    }

    /// <summary>
    /// 首次握手失败的插件按重启策略重试，成功后补登记声明。
    /// </summary>
    private void RetryDiscovery(PluginRuntime runtime)
    {
        runtime.Discovered = RegisterLate;
        runtime.ScheduleRestart();
    }

    /// <summary>
    /// 重试中才握手成功的插件：把代理操作登记进操作登记表。
    /// </summary>
    /// <remarks>
    /// 与运行期加载一致，代理操作只对内核调用器可见（协议适配器的路由在启动时已经发布）；订阅与消息目录要在配置阶段接进容器，宿主重启后才生效，这里记警告。
    /// </remarks>
    private Result RegisterLate(PluginRuntime runtime)
    {
        var registered = Register(runtime);
        if (registered.IsSuccess && runtime.Handshake is { } handshake && (handshake.Subscriptions.Count > 0 || handshake.Catalogs.Count > 0))
        {
            LogLateDeclarations(_logger, runtime.Manifest.Id, handshake.Subscriptions.Count, handshake.Catalogs.Count);
        }

        return registered;
    }

    /// <summary>
    /// 启动任何插件代码之前的检查：权限、入口与可执行文件。
    /// </summary>
    /// <remarks>
    /// 通过时返回空并给出可执行文件路径。
    /// </remarks>
    private static Error? Preflight(PluginSource source, Manifest manifest, out string? executable)
    {
        executable = null;
        var missing = source.Permissions.Missing(manifest);
        if (missing.Count > 0)
        {
            return Error.Denied(PluginErrors.PermissionDenied, PluginMessages.PermissionsNotGranted, MessageArguments.Of(("id", manifest.Id), ("permissions", string.Join(", ", missing))));
        }

        if (string.IsNullOrWhiteSpace(manifest.Entry))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.EntryRequired, MessageArguments.Of(("id", manifest.Id)));
        }

        executable = ResolveExecutable(source.Directory, manifest.Entry);
        return executable is null
            ? Error.NotFound(PluginErrors.NotFound, PluginMessages.ExecutableMissing, MessageArguments.Of(("path", Path.Combine(source.Directory, manifest.Entry))))
            : null;
    }

    /// <summary>
    /// 以发现时被拒登记：不启动进程，插件停在 Failed（<c>lifecycle.md</c> §1）。
    /// </summary>
    /// <remarks>
    /// 返回拒绝的原因；同一清单 ID 已登记时返回冲突。
    /// </remarks>
    private async Task<Result<PluginRuntime>> RejectAsync(PluginSource source, Manifest manifest, Error error, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_plugins.ContainsKey(manifest.Id))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, PluginMessages.AlreadyLoaded, MessageArguments.Of(("id", manifest.Id)));
            }

            var runtime = new PluginRuntime(manifest, source.Directory, string.Empty, source.Permissions, _context, _loggerFactory.CreateLogger<PluginRuntime>());
            runtime.Reject(error);
            _plugins[manifest.Id] = runtime;
            _order.Add(runtime);
        }
        finally
        {
            _gate.Release();
        }

        return error;
    }

    /// <summary>
    /// 把运行时握手声明的操作登记为代理操作。
    /// </summary>
    private Result Register(PluginRuntime runtime)
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
    private void Remove(PluginRuntime runtime)
    {
        _plugins.TryRemove(runtime.Manifest.Id, out _);
        lock (_order)
        {
            _order.Remove(runtime);
        }
    }

    /// <summary>
    /// 读目录里的清单并确认是进程外插件。
    /// </summary>
    private static Result<Manifest> ReadManifest(string directory)
    {
        var read = ReadManifestFile(directory);
        if (!read.TryGetValue(out var manifest))
        {
            return read.Error!;
        }

        var kind = CheckKind(manifest);
        return kind.IsSuccess ? manifest : kind.Error!;
    }

    /// <summary>
    /// 读目录里的清单文件。
    /// </summary>
    private static Result<Manifest> ReadManifestFile(string directory)
    {
        var read = ManifestReader.ReadFile(Path.Combine(directory, ManifestReader.FileName));
        return read.TryGetValue(out var manifest)
            ? manifest
            : new Error(PluginErrors.InvalidSource, PluginMessages.ManifestInvalid, MessageArguments.Of(("directory", directory), ("reason", read.Error!.Message)), read.Error);
    }

    /// <summary>
    /// 清单是否描述一个进程外插件。
    /// </summary>
    private static Result CheckKind(Manifest manifest)
        => manifest.Kind == ManifestKind.Plugin && manifest.Isolation == PluginIsolation.OutOfProcess
            ? Result.Success()
            : new Error(PluginErrors.InvalidSource, PluginMessages.NotOutOfProcess, MessageArguments.Of(("id", manifest.Id), ("kind", manifest.Kind.ToString()), ("isolation", manifest.Isolation?.ToString())));

    /// <summary>
    /// 入口相对插件目录。
    /// </summary>
    /// <remarks>
    /// Windows 上没有扩展名时补 <c>.exe</c>。不存在返回空。
    /// </remarks>
    internal static string? ResolveExecutable(string directory, string entry)
    {
        var path = Path.GetFullPath(Path.Combine(directory, entry));
        if (File.Exists(path))
        {
            return path;
        }

        if (OperatingSystem.IsWindows() && !Path.HasExtension(path) && File.Exists(path + ".exe"))
        {
            return path + ".exe";
        }

        return null;
    }

    /// <summary>
    /// 记录回调服务器已监听。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin host callback server is listening on {Endpoint}.")]
    private static partial void LogServer(ILogger logger, PluginEndpoint endpoint);

    /// <summary>
    /// 记录发现阶段失败的插件（宿主继续运行）。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {Plugin} failed during discovery: {Code} {Message}")]
    private static partial void LogDiscoveryFailed(ILogger logger, string plugin, string code, string message);

    /// <summary>
    /// 记录重试中才握手成功的插件声明了无法补登记的订阅或消息目录。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} completed its first handshake only after startup; its {Subscriptions} subscriptions and {Catalogs} message catalogs take effect after the host restarts.")]
    private static partial void LogLateDeclarations(ILogger logger, string pluginId, int subscriptions, int catalogs);

    /// <summary>
    /// 记录插件启动失败（宿主继续运行）。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} failed to start: {Code} {Message}")]
    private static partial void LogStartFailed(ILogger logger, string pluginId, string code, string message);
}
