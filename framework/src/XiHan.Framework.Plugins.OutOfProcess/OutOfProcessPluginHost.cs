// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.OutOfProcess.Callbacks;
using XiHan.Framework.Plugins.OutOfProcess.Runtime;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Handshake;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Plugins.OutOfProcess;

/// <summary>
/// 进程外插件宿主（<c>isolation: out-of-process</c>，RFC-0017）
/// </summary>
/// <remarks>
/// 按目录发现插件、启动并握手、把声明的操作与订阅接进宿主、监视健康与崩溃重启、运行宿主回调服务，并把每个插件作为健康条目上报。通常由 <see cref="OutOfProcessPluginsFeature"/> 装配；也可按 <see cref="IPluginHost"/> 在运行期加载与卸载
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class OutOfProcessPluginHost : IPluginHost, IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 回调 <c>Host.Invoke</c> 需要的权限
    /// </summary>
    public const string PipelinePermission = PluginCallbackPermissions.Pipeline;

    /// <summary>
    /// 回调 <c>Host.Publish</c> 需要的权限
    /// </summary>
    public const string MessagingPermission = PluginCallbackPermissions.Messaging;

    /// <summary>
    /// 回调时宿主所用主体的认证方案
    /// </summary>
    public const string PluginScheme = PluginIdentity.Scheme;

    /// <summary>
    /// 健康条目名前缀
    /// </summary>
    public const string HealthPrefix = PluginHealth.EntryPrefix;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly OutOfProcessPluginOptions _options;

    /// <summary>
    /// 会话登记表
    /// </summary>
    private readonly PluginSessions _sessions = new();

    /// <summary>
    /// 插件名册
    /// </summary>
    /// <remarks>
    /// 运行时进出宿主、目录、权限码与代理操作的登记与撤回、事件投递与健康条目都经它；加载卸载锁与登记锁在它里面（ADR-0085、ADR-0260）
    /// </remarks>
    private readonly PluginRoster<PluginRuntime> _roster = new();

    /// <summary>
    /// 插件共享的宿主环境
    /// </summary>
    private readonly PluginHostContext _context;

    /// <summary>
    /// 日志工厂
    /// </summary>
    /// <remarks>
    /// 附着到容器后换成宿主的
    /// </remarks>
    private ILoggerFactory _loggerFactory;

    /// <summary>
    /// 日志
    /// </summary>
    private ILogger _logger;

    /// <summary>
    /// 回调服务器
    /// </summary>
    /// <remarks>
    /// 端点在创建宿主时选定并占住（配置阶段启动的插件经环境变量拿到它们），附着后才监听；停止时释放
    /// </remarks>
    private PluginCallbackServer? _server;

    /// <summary>
    /// 创建宿主
    /// </summary>
    /// <remarks>
    /// 回调端点在此刻选定（启动插件时经环境变量下发），回环 TCP 的端口由系统分配并在此刻占住；服务器在 <see cref="AttachAsync"/> 时才监听
    /// </remarks>
    /// <param name="options">选项</param>
    /// <param name="configuration">宿主配置（插件配置节与绑定从中读取）</param>
    /// <param name="clock">时钟</param>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="stopGrace">停止宽限（通常取内核的停止超时）</param>
    /// <param name="defaultLocale">宿主默认区域</param>
    public OutOfProcessPluginHost(OutOfProcessPluginOptions options, IConfiguration configuration, TimeProvider clock, ILoggerFactory loggerFactory, TimeSpan stopGrace, string defaultLocale = "en-US")
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLocale);
        _logger = loggerFactory.CreateLogger<OutOfProcessPluginHost>();
        var server = PluginCallbackServer.Reserve();
        _server = server;
        HostEndpoints = server.Endpoints;
        _context = new PluginHostContext(
            HostEndpoints,
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
    /// 宿主回调端点，按偏好排序
    /// </summary>
    /// <remarks>
    /// Windows 上是只允许当前用户的命名管道加回环 TCP，其他系统上是私有目录里的 Unix 域套接字加回环 TCP（临时目录的路径含空白或 <c>;</c> 时只有回环 TCP）；回环 TCP 总在最后（<c>handshake.md</c> §4，ADR-0237）。以 <c>;</c> 分隔写进插件的 <c>XIHAN_PLUGIN_HOST_ADDRESS</c>，第一项写进 <c>HandshakeRequest.host_endpoint</c>
    /// </remarks>
    public IReadOnlyList<PluginEndpoint> HostEndpoints { get; }

    /// <inheritdoc />
    public IReadOnlyList<PluginStatus> Plugins => _roster.Statuses;

    /// <summary>
    /// 查找一个插件的诊断快照
    /// </summary>
    /// <remarks>
    /// 未加载时无值
    /// </remarks>
    public Maybe<OutOfProcessPluginStatus> FindStatus(string manifestId)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        return _roster.TryFind(manifestId, out var runtime) ? runtime.Describe() : Maybe.None<OutOfProcessPluginStatus>();
    }

    /// <summary>
    /// 指定代理操作的登记表
    /// </summary>
    internal void UseRegistry(OperationRegistry registry) => _roster.UseRegistry(registry);

    /// <summary>
    /// 同步发现一个插件目录（配置阶段使用）
    /// </summary>
    /// <remarks>
    /// 读清单、按选项授予权限、启动并握手。失败的插件仍登记在宿主里以 Failed 上报：发现时被拒的（清单、权限、入口不合格）可描述、是健康条目，清单读不出来的只作为健康条目 <c>plugins.&lt;目录名&gt;</c>（<c>lifecycle.md</c> §1）；附着到容器时逐个记日志
    /// </remarks>
    internal Result<PluginRuntime> Discover(string directory)
        => DiscoverAsync(directory, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<Result<PluginStatus>> LoadAsync(PluginSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var manifest = PluginManifests.Read(source.Directory, PluginIsolation.OutOfProcess);
        if (!manifest.TryGetValue(out var read))
        {
            return manifest.Error!;
        }

        var discovered = await DiscoverAsync(source, read, atStartup: false, cancellationToken).ConfigureAwait(false);
        if (!discovered.TryGetValue(out var runtime))
        {
            return discovered.Error!;
        }

        // 目录、权限码与代理操作一次登记：HTTP 路由、接口文档与 MCP tool 跟随操作登记表随之出现（ADR-0251、ADR-0252）
        var registered = _roster.Register(runtime, null);
        if (registered.IsFailure)
        {
            await runtime.StopAsync("unload", cancellationToken).ConfigureAwait(false);
            _roster.Remove(runtime);
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
    /// <remarks>
    /// 先按归属（清单 ID）撤回插件登记的代理操作、权限码与消息目录，再停止进程（<c>plugin/invocation.md</c> §1，ADR-0085、ADR-0252）：撤回之后按 ID 调用这些操作返回 <c>KERNEL_NOTFOUND_001</c>，已经在执行的调用照常完成或在进程停止后以 <c>PLUGINS_UNAVAILABLE_001</c> 结束；卸载完成后可以直接 <see cref="LoadAsync"/> 同一个插件。订阅在配置阶段接进容器，不随卸载撤回
    /// </remarks>
    public async Task<Result> UnloadAsync(string manifestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        // 先撤回再停止进程：新的调用立刻得到未登记，不再排进正在停止的进程
        var withdrawn = await _roster.WithdrawAsync(manifestId, null, cancellationToken).ConfigureAwait(false);
        if (!withdrawn.TryGetValue(out var runtime))
        {
            return withdrawn.Error!;
        }

        await runtime.StopAsync("unload", cancellationToken).ConfigureAwait(false);
        runtime.Dispose();
        return Result.Success();
    }

    /// <summary>
    /// 附着到已构建的宿主容器
    /// </summary>
    /// <remarks>
    /// 换用宿主日志与时钟、取得目录登记表、启动回调服务器、记录发现阶段失败的插件、启动 <c>onStartup</c> 的插件。握手失败与启动失败的插件按重启策略在后台重试（<c>lifecycle.md</c> §6），发现时被拒的不重试。目录登记表在这里才建立，配置阶段注册的插件目录与同名目录冲突时这里抛出，宿主启动失败
    /// </remarks>
    internal async Task AttachAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        _context.Clock = services.GetService<TimeProvider>() ?? _context.Clock;
        _roster.UseCatalogs(services.GetRequiredService<MessageCatalogRegistry>());
        _loggerFactory = services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<OutOfProcessPluginHost>();
        foreach (var runtime in _roster.Runtimes)
        {
            runtime.Logger = _loggerFactory.CreateLogger<PluginRuntime>();
        }

        var server = _server ?? throw new ObjectDisposedException(nameof(OutOfProcessPluginHost));
        await server.StartAsync(_sessions, services, _loggerFactory, cancellationToken).ConfigureAwait(false);
        LogServer(_logger, server);

        // 配置阶段还没有宿主日志：清单读不出来、发现时被拒与握手失败的插件在这里补记
        foreach (var (directory, error) in _roster.Unreadable)
        {
            LogDiscoveryFailed(_logger, directory, error.Code, error.Message);
        }

        foreach (var runtime in _roster.Runtimes)
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
    /// 停止全部插件（与登记相反的顺序）并关闭回调服务器
    /// </summary>
    internal async Task StopAllAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in Enumerable.Reverse(_roster.Runtimes))
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
    /// 把一条消息投递给订阅了该主题与类型的插件
    /// </summary>
    /// <remarks>
    /// 第一个失败即为结果
    /// </remarks>
    internal ValueTask<Result> DeliverAsync(MessageDelivery delivery, InvocationContext context)
        => _roster.DeliverAsync(delivery, context);

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check() => _roster.Check();

    /// <summary>
    /// 已释放
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 停止全部插件并释放
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAllAsync(CancellationToken.None).ConfigureAwait(false);
        foreach (var runtime in _roster.Runtimes)
        {
            runtime.Dispose();
        }

        _roster.Dispose();
    }

    /// <summary>
    /// 按目录发现：读清单、按选项取授予
    /// </summary>
    private async Task<Result<PluginRuntime>> DiscoverAsync(string directory, CancellationToken cancellationToken)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var file = PluginManifests.ReadFile(full);
        if (!file.TryGetValue(out var read))
        {
            // 没有清单 ID：只能按目录名上报
            _roster.AddUnreadable(full, file.Error!);
            return file.Error!;
        }

        var source = new PluginSource(full, _options.GrantsFor(read.Id));
        var kind = PluginManifests.CheckKind(read, PluginIsolation.OutOfProcess);
        if (kind.IsFailure)
        {
            return await RejectAsync(source, read, kind.Error!, cancellationToken).ConfigureAwait(false);
        }

        return await DiscoverAsync(source, read, atStartup: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 校验权限与可执行文件，登记运行时并探测启动
    /// </summary>
    /// <remarks>
    /// <paramref name="atStartup"/> 为真（配置阶段按目录发现）时被拒的插件以 Failed 登记、握手失败的重试留到附着到容器之后；为假（运行期加载）时被拒只把错误返回给调用方，握手失败的插件保持 Failed 并立即按重启策略重试（<c>lifecycle.md</c> §6）
    /// </remarks>
    private async Task<Result<PluginRuntime>> DiscoverAsync(PluginSource source, Manifest manifest, bool atStartup, CancellationToken cancellationToken)
    {
        // 权限在启动任何代码之前判定：清单声明了而宿主未授予的权限，一项也不能少
        var rejected = Preflight(source, manifest, out var executable);
        if (rejected is not null)
        {
            return atStartup ? await RejectAsync(source, manifest, rejected, cancellationToken).ConfigureAwait(false) : rejected;
        }

        var added = await _roster.AddAsync(manifest.Id, () => new PluginRuntime(manifest, source.Directory, executable!, source.Permissions, _context, _loggerFactory.CreateLogger<PluginRuntime>()), cancellationToken).ConfigureAwait(false);
        if (!added.TryGetValue(out var runtime))
        {
            return added.Error!;
        }

        var discovered = await runtime.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        if (discovered.IsFailure && !atStartup)
        {
            RetryDiscovery(runtime);
        }

        return discovered.IsSuccess ? runtime : discovered.Error!;
    }

    /// <summary>
    /// 首次握手失败的插件按重启策略重试，成功后补登记声明
    /// </summary>
    private void RetryDiscovery(PluginRuntime runtime)
    {
        runtime.Discovered = RegisterLate;
        runtime.ScheduleRestart();
    }

    /// <summary>
    /// 重试中才握手成功的插件：把代理操作登记进操作登记表
    /// </summary>
    /// <remarks>
    /// 与运行期加载一致，目录、权限码与代理操作一次登记；订阅要在配置阶段接进容器，宿主重启后才生效，这里记警告
    /// </remarks>
    private Result RegisterLate(PluginRuntime runtime)
    {
        var registered = _roster.Register(runtime, null);
        if (registered.IsSuccess && runtime.Handshake is { Subscriptions.Count: > 0 } handshake)
        {
            LogLateSubscriptions(_logger, runtime.Manifest.Id, handshake.Subscriptions.Count);
        }

        return registered;
    }

    /// <summary>
    /// 启动任何插件代码之前的检查：清单的贡献、权限、入口与可执行文件
    /// </summary>
    /// <remarks>
    /// 通过时返回空并给出可执行文件路径
    /// </remarks>
    private static Error? Preflight(PluginSource source, Manifest manifest, out string? executable)
    {
        executable = null;

        // 只有 Wasm 插件能贡献中间件（RFC-0017「范围」）：清单写了中间件而宿主照常加载，作者会以为它生效了
        // 与隔离级别不符同属清单不合格，先于权限判定，按发现时被拒处理（lifecycle.md §1），不静默忽略
        if (manifest.Contributes.Pipeline.Count > 0)
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.PipelineUnsupported, MessageArguments.Of(("id", manifest.Id), ("middleware", string.Join(", ", manifest.Contributes.Pipeline.Select(static p => p.Middleware)))));
        }

        // 进程外插件的权限只看宿主授予；授予与入口的核对两个宿主相同
        var checkedEntry = PluginPreflight.Check(source, manifest, source.Permissions.IsGranted);
        if (!checkedEntry.TryGetValue(out var entry))
        {
            return checkedEntry.Error;
        }

        executable = ResolveExecutable(source.Directory, entry);
        return executable is null ? PluginPreflight.EntryMissing(Path.Combine(source.Directory, entry)) : null;
    }

    /// <summary>
    /// 以发现时被拒登记：不启动进程，插件停在 Failed（<c>lifecycle.md</c> §1）
    /// </summary>
    /// <remarks>
    /// 返回拒绝的原因；同一清单 ID 已登记时返回冲突
    /// </remarks>
    private async Task<Result<PluginRuntime>> RejectAsync(PluginSource source, Manifest manifest, Error error, CancellationToken cancellationToken)
    {
        var added = await _roster.AddAsync(
            manifest.Id,
            () =>
            {
                var runtime = new PluginRuntime(manifest, source.Directory, string.Empty, source.Permissions, _context, _loggerFactory.CreateLogger<PluginRuntime>());
                runtime.Reject(error);
                return runtime;
            },
            cancellationToken).ConfigureAwait(false);
        return added.IsSuccess ? error : added.Error!;
    }

    /// <summary>
    /// 入口相对插件目录
    /// </summary>
    /// <remarks>
    /// Windows 上没有扩展名时补 <c>.exe</c>。不存在返回空
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
    /// 记录回调服务器已监听
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin host callback server is listening on {Endpoints}.")]
    private static partial void LogServer(ILogger logger, PluginCallbackServer endpoints);

    /// <summary>
    /// 记录发现阶段失败的插件（宿主继续运行）
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {Plugin} failed during discovery: {Code} {Message}")]
    private static partial void LogDiscoveryFailed(ILogger logger, string plugin, string code, string message);

    /// <summary>
    /// 记录重试中才握手成功的插件声明了无法补登记的订阅
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Plugin {PluginId} completed its first handshake only after startup; its {Subscriptions} subscriptions take effect after the host restarts.")]
    private static partial void LogLateSubscriptions(ILogger logger, string pluginId, int subscriptions);

    /// <summary>
    /// 记录插件启动失败（宿主继续运行）
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin {PluginId} failed to start: {Code} {Message}")]
    private static partial void LogStartFailed(ILogger logger, string pluginId, string code, string message);
}
