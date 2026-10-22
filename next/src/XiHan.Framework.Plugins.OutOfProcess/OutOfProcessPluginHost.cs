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
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 进程外插件宿主（<c>isolation: out-of-process</c>，RFC-0017）：按目录发现插件、启动并握手、把声明的操作与订阅接进宿主、监视健康与崩溃重启、
/// 运行宿主回调服务，并把每个插件作为健康条目上报。通常由 <see cref="OutOfProcessPluginsFeature"/> 装配；也可按 <see cref="IPluginHost"/> 在运行期加载与卸载。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class OutOfProcessPluginHost : IPluginHost, IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 回调 <c>Host.Invoke</c> 需要的权限。
    /// </summary>
    public const string PipelinePermission = "capability:kernel.pipeline";

    /// <summary>
    /// 回调 <c>Host.Publish</c> 需要的权限。
    /// </summary>
    public const string MessagingPermission = "capability:messaging.queue";

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
    /// 串行化加载与卸载。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 插件共享的宿主环境。
    /// </summary>
    private readonly PluginHostContext _context;

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
    /// 回调服务器；附着后启动。
    /// </summary>
    private PluginCallbackServer? _server;

    /// <summary>
    /// 创建宿主。回调端点在此刻选定（启动插件时经环境变量下发），服务器在 <see cref="AttachAsync"/> 时才监听。
    /// </summary>
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
    /// 一个插件的诊断快照；未加载为空。
    /// </summary>
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
    /// 同步发现一个插件目录（配置阶段使用）：读清单、按选项授予权限、启动并握手。失败的插件仍登记在宿主里以 Failed 上报。
    /// </summary>
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

        var discovered = await DiscoverAsync(source, read, cancellationToken).ConfigureAwait(false);
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
        return started.IsSuccess ? runtime.Status : started.Error!;
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
    /// 附着到已构建的宿主容器：换用宿主日志、启动回调服务器、启动 <c>onStartup</c> 的插件（失败只记日志）。
    /// </summary>
    internal async Task AttachAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _loggerFactory = services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<OutOfProcessPluginHost>();
        foreach (var runtime in _order)
        {
            runtime.Logger = _loggerFactory.CreateLogger<PluginRuntime>();
        }

        _server = new PluginCallbackServer(HostEndpoint, _sessions, services, _loggerFactory);
        await _server.StartAsync(cancellationToken).ConfigureAwait(false);
        LogServer(_logger, HostEndpoint);

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
    /// 停止全部插件并释放。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
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
        var full = Path.GetFullPath(directory);
        var manifest = ReadManifest(full);
        if (!manifest.TryGetValue(out var read))
        {
            return manifest.Error!;
        }

        return await DiscoverAsync(new PluginSource(full, _options.GrantsFor(read.Id)), read, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 校验权限与可执行文件，登记运行时并探测启动。
    /// </summary>
    private async Task<Result<PluginRuntime>> DiscoverAsync(PluginSource source, Manifest manifest, CancellationToken cancellationToken)
    {
        // 权限在启动任何代码之前判定：清单声明了而宿主未授予的权限，一项也不能少
        var missing = source.Permissions.Missing(manifest);
        if (missing.Count > 0)
        {
            return Error.Denied(PluginErrors.PermissionDenied, PluginMessages.PermissionsNotGranted, MessageArguments.Of(("id", manifest.Id), ("permissions", string.Join(", ", missing))));
        }

        if (string.IsNullOrWhiteSpace(manifest.Entry))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.EntryRequired, MessageArguments.Of(("id", manifest.Id)));
        }

        var executable = ResolveExecutable(source.Directory, manifest.Entry);
        if (executable is null)
        {
            return Error.NotFound(PluginErrors.NotFound, PluginMessages.ExecutableMissing, MessageArguments.Of(("path", Path.Combine(source.Directory, manifest.Entry))));
        }

        PluginRuntime runtime;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_plugins.ContainsKey(manifest.Id))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, PluginMessages.AlreadyLoaded, MessageArguments.Of(("id", manifest.Id)));
            }

            runtime = new PluginRuntime(manifest, source.Directory, executable, source.Permissions, _context, _loggerFactory.CreateLogger<PluginRuntime>());
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
        var read = ManifestReader.ReadFile(Path.Combine(directory, ManifestReader.FileName));
        if (!read.TryGetValue(out var manifest))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.ManifestInvalid, MessageArguments.Of(("directory", directory), ("reason", read.Error!.Message)), read.Error);
        }

        if (manifest.Kind != ManifestKind.Plugin || manifest.Isolation != PluginIsolation.OutOfProcess)
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.NotOutOfProcess, MessageArguments.Of(("id", manifest.Id), ("kind", manifest.Kind.ToString()), ("isolation", manifest.Isolation?.ToString())));
        }

        return manifest;
    }

    /// <summary>
    /// 入口相对插件目录；Windows 上没有扩展名时补 <c>.exe</c>。不存在返回空。
    /// </summary>
    private static string? ResolveExecutable(string directory, string entry)
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

    /// <summary>记录回调服务器已监听。</summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin host callback server is listening on {Endpoint}.")]
    private static partial void LogServer(ILogger logger, PluginEndpoint endpoint);

    /// <summary>记录插件启动失败（宿主继续运行）。</summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} failed to start: {Code} {Message}")]
    private static partial void LogStartFailed(ILogger logger, string pluginId, string code, string message);
}
