// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wasmtime;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Plugins.Wasm.Instances;
using XiHan.Framework.Plugins.Wasm.Runtime;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Plugins.Wasm;

/// <summary>
/// Wasm 插件宿主（<c>isolation: wasm</c>，RFC-0018，Experimental）
/// </summary>
/// <remarks>
/// 按目录发现插件、编译并握手、把声明的操作与订阅接进宿主、健康轮询与崩溃重建，并把每个插件作为健康条目上报。通常由 <see cref="WasmPluginsFeature"/> 装配；也可按 <see cref="IPluginHost"/> 在运行期加载与卸载，插件贡献的中间件随加载成组登记进管道、随卸载按归属撤回（ADR-0088）
/// </remarks>
[ApiLevel(Stability.Experimental, "5.0")]
public sealed partial class WasmPluginHost : IPluginHost, IHealthContributor, IAsyncDisposable
{
    /// <summary>
    /// 健康条目名前缀
    /// </summary>
    public const string HealthPrefix = PluginHealth.EntryPrefix;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly WasmPluginOptions _options;

    /// <summary>
    /// 插件名册
    /// </summary>
    /// <remarks>
    /// 运行时进出宿主、目录、权限码与代理操作的登记与撤回、事件投递与健康条目都经它；加载卸载锁与登记锁在它里面，中间件的登记与撤回作为追加项在登记锁内进行（ADR-0085、ADR-0088、ADR-0260）
    /// </remarks>
    private readonly PluginRoster<WasmPluginRuntime> _roster = new();

    /// <summary>
    /// 插件共享的宿主环境
    /// </summary>
    private readonly WasmHostContext _context;

    /// <summary>
    /// epoch 计时器：按间隔递增引擎 epoch，让各实例的截止时间生效
    /// </summary>
    private readonly CancellationTokenSource _ticker = new();

    /// <summary>
    /// 计时循环
    /// </summary>
    /// <remarks>
    /// 释放时先等它退出，引擎才能安全释放
    /// </remarks>
    private readonly Task _ticking;

    /// <summary>
    /// 已释放
    /// </summary>
    private bool _disposed;

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
    /// 插件中间件登记到的管道
    /// </summary>
    /// <remarks>
    /// 由特性在配置阶段提供；运行期加载经它成组登记插件贡献的中间件，卸载经它按归属撤回（ADR-0088）
    /// </remarks>
    private PipelineBuilder? _pipeline;

    /// <summary>
    /// 创建宿主：建一个启用 epoch 中断的引擎并开始计时
    /// </summary>
    /// <param name="options">选项</param>
    /// <param name="configuration">宿主配置</param>
    /// <param name="clock">时钟</param>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="defaultLocale">宿主默认区域</param>
    /// <exception cref="FrameworkException">宿主进程强制用户态影子栈（CET），Wasm 陷阱会终止进程；宿主可执行项目须设置 <c>CETCompat=false</c></exception>
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
    public IReadOnlyList<PluginStatus> Plugins => _roster.Statuses;

    /// <summary>
    /// 查找一个插件的诊断快照
    /// </summary>
    /// <remarks>
    /// 未加载时无值
    /// </remarks>
    public Maybe<WasmPluginStatus> FindStatus(string manifestId)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        return _roster.TryFind(manifestId, out var runtime) ? runtime.Describe() : Maybe.None<WasmPluginStatus>();
    }

    /// <summary>
    /// 指定代理操作的登记表
    /// </summary>
    internal void UseRegistry(OperationRegistry registry) => _roster.UseRegistry(registry);

    /// <summary>
    /// 指定插件中间件的管道
    /// </summary>
    internal void UsePipeline(PipelineBuilder pipeline) => _pipeline = pipeline;

    /// <summary>
    /// 把插件清单贡献的中间件登记进管道并声明相对次序
    /// </summary>
    /// <remarks>
    /// 每个贡献项一个实例，以 <c>&lt;清单 ID&gt;:&lt;中间件名&gt;</c> 为名登记在所在阶段末尾，归属为清单 ID（<c>spec/plugin/wasm.md</c> §6）。<c>before</c> / <c>after</c> 的每一项先按本插件的中间件名解析，否则原样作为管道里的中间件名（另一个插件的 <c>&lt;清单 ID&gt;:&lt;中间件名&gt;</c>，或只登记一次的宿主中间件类型全名）。配置阶段直接登记，目标在构建管道时解析；运行期加载时在 <see cref="PipelineBuilder.UseOwned"/> 里登记、立即核对。约束只声明在本插件的中间件上，按归属撤回时一并撤掉
    /// </remarks>
    internal static void AddMiddleware(PipelineBuilder pipeline, WasmPluginRuntime runtime)
    {
        var contributions = runtime.Manifest.Contributes.Pipeline;
        var own = contributions.Select(static c => c.Middleware).ToHashSet(StringComparer.Ordinal);
        string Name(string middleware) => $"{runtime.Manifest.Id}:{middleware}";
        string Target(string reference) => own.Contains(reference) ? Name(reference) : reference;

        foreach (var contribution in contributions)
        {
            pipeline.UseNamedInstance(Name(contribution.Middleware), new WasmMiddleware(runtime, contribution.Middleware, contribution.Stage), contribution.Stage, Placement.End, runtime.Manifest.Id);
        }

        // 约束在本插件的中间件都登记之后声明：互相引用的两项不论清单里谁先谁后都能找到对方
        foreach (var contribution in contributions)
        {
            foreach (var later in contribution.Before)
            {
                pipeline.Before(Name(contribution.Middleware), Target(later));
            }

            foreach (var earlier in contribution.After)
            {
                pipeline.After(Name(contribution.Middleware), Target(earlier));
            }
        }
    }

    /// <summary>
    /// 同步发现一个插件目录（配置阶段使用）
    /// </summary>
    /// <remarks>
    /// 读清单、按选项授予权限与映射、编译并握手。失败的插件仍登记在宿主里以 Failed 上报：发现时被拒的（清单、权限、模块文件不合格）可描述、是健康条目，清单读不出来的只作为健康条目 <c>plugins.&lt;目录名&gt;</c>（<c>lifecycle.md</c> §1）；附着到容器时逐个记日志
    /// </remarks>
    internal Result<WasmPluginRuntime> Discover(string directory)
        => DiscoverAsync(directory, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    /// <remarks>
    /// 消息目录、代理操作、声明的权限码与贡献的中间件一次登记、全进或全不进（ADR-0085、ADR-0088、ADR-0252）：中间件在 <see cref="PipelineBuilder.UseOwned"/> 里成组登记并立即核对，约束目标未登记、有歧义、跨阶段（<c>KERNEL_PIPELINE_003</c>）或成环（<c>KERNEL_PIPELINE_001</c>）时整个插件登记失败、已登记的目录、操作与权限码撤回、管道不变。登记成功之后开始的调用经过插件的中间件
    /// </remarks>
    public async Task<Result<PluginStatus>> LoadAsync(PluginSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var manifest = PluginManifests.Read(source.Directory, PluginIsolation.Wasm);
        if (!manifest.TryGetValue(out var read))
        {
            return manifest.Error!;
        }

        var discovered = await DiscoverAsync(source, read, _options.MountsFor(read.Id), keepRejected: false, cancellationToken).ConfigureAwait(false);
        if (!discovered.TryGetValue(out var runtime))
        {
            return discovered.Error!;
        }

        // 操作先进、中间件后进，都在名册的登记锁里：中间件进不去时目录、操作与权限码一并撤回
        var registered = _roster.Register(runtime, UseMiddleware);
        if (registered.IsFailure)
        {
            await runtime.StopAsync("unload", cancellationToken).ConfigureAwait(false);
            _roster.Remove(runtime);
            return registered.Error!;
        }

        var started = await runtime.StartAsync(cancellationToken).ConfigureAwait(false);
        return started.IsSuccess ? runtime.Status : started.Error!;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 先按归属（清单 ID）撤回插件贡献的中间件、登记的代理操作、权限码与消息目录，再停止并丢弃实例（<c>plugin/invocation.md</c> §1、<c>plugin/wasm.md</c> §6，ADR-0085、ADR-0088、ADR-0252）：撤回之后开始的调用不再经过它的中间件，按 ID 调用它的操作返回 <c>KERNEL_NOTFOUND_001</c>；撤回之前已经进入管道的调用照常完成，或在插件停止后以 <c>PLUGINS_UNAVAILABLE_001</c> 结束。卸载完成后可以直接 <see cref="LoadAsync"/> 同一个插件。别的中间件以次序约束引用了它的中间件时卸载被拒（<c>KERNEL_PIPELINE_003</c>），什么都不撤、插件照常服务。订阅在配置阶段接进容器，不随卸载撤回
    /// </remarks>
    public async Task<Result> UnloadAsync(string manifestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        // 先撤回再停止实例，新的调用立刻不再经过它的中间件、得到操作未登记
        // 中间件最先撤：别的中间件以次序约束引用了它时撤不了，这时名册什么都不改、运行时照常服务
        var withdrawn = await _roster.WithdrawAsync(manifestId, RemoveMiddleware, cancellationToken).ConfigureAwait(false);
        if (!withdrawn.TryGetValue(out var runtime))
        {
            return withdrawn.Error!;
        }

        await runtime.StopAsync("unload", cancellationToken).ConfigureAwait(false);
        // 撤回之前已经进入管道的调用可能还在等运行时的锁：等它们按不可用离开再释放
        await runtime.DrainAsync().ConfigureAwait(false);
        runtime.Dispose();
        return Result.Success();
    }

    /// <summary>
    /// 附着到已构建的宿主容器
    /// </summary>
    /// <remarks>
    /// 换用宿主日志与时钟、开放回调、记录发现阶段失败的插件、启动 <c>onStartup</c> 的插件（失败只记日志）
    /// </remarks>
    internal async Task AttachAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        // 在开放任何调用之前换钟：截止时间由 Resilience 阶段按容器的时钟落为定时取消，调用预算与回调计时必须用同一个时钟
        _context.Clock = services.GetService<TimeProvider>() ?? _context.Clock;
        // 目录登记表在这里才建立：配置阶段注册的插件目录与同名目录冲突时这里抛出，宿主启动失败
        _roster.UseCatalogs(services.GetRequiredService<MessageCatalogRegistry>());
        _loggerFactory = services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<WasmPluginHost>();
        foreach (var runtime in _roster.Runtimes)
        {
            runtime.Logger = _loggerFactory.CreateLogger<WasmPluginRuntime>();
        }

        _context.Services = services;
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
            }
        }
    }

    /// <summary>
    /// 停止全部插件（与登记相反的顺序）
    /// </summary>
    internal async Task StopAllAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in Enumerable.Reverse(_roster.Runtimes))
        {
            await runtime.StopAsync("shutdown", cancellationToken).ConfigureAwait(false);
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
    /// 停止全部插件、停止 epoch 计时并释放引擎
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
            await runtime.DrainAsync().ConfigureAwait(false);
            runtime.Dispose();
        }

        // 计时器可能正在调用引擎：先停它再释放引擎
        await _ticker.CancelAsync().ConfigureAwait(false);
        await _ticking.ConfigureAwait(false);
        _ticker.Dispose();
        _context.Engine.Dispose();
        _roster.Dispose();
    }

    /// <summary>
    /// 按间隔递增 epoch
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
    /// 按目录发现：读清单、按选项取授予与映射
    /// </summary>
    private async Task<Result<WasmPluginRuntime>> DiscoverAsync(string directory, CancellationToken cancellationToken)
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
        var mounts = _options.MountsFor(read.Id);
        var kind = PluginManifests.CheckKind(read, PluginIsolation.Wasm);
        if (kind.IsFailure)
        {
            return await RejectAsync(source, read, mounts, kind.Error!, cancellationToken).ConfigureAwait(false);
        }

        return await DiscoverAsync(source, read, mounts, keepRejected: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 校验权限（<c>capability:</c> 须授予、<c>fs.*</c> 须映射、<c>net.outbound:</c> 永不授予）与模块文件
    /// </summary>
    /// <remarks>
    /// 登记运行时并探测启动。<paramref name="keepRejected"/> 为真（按目录发现）时被拒的插件以 Failed 登记，否则（运行期加载）只把错误返回给调用方
    /// </remarks>
    private async Task<Result<WasmPluginRuntime>> DiscoverAsync(PluginSource source, Manifest manifest, IReadOnlyDictionary<string, string> mounts, bool keepRejected, CancellationToken cancellationToken)
    {
        var rejected = Preflight(source, manifest, mounts, out var modulePath);
        if (rejected is not null)
        {
            return keepRejected ? await RejectAsync(source, manifest, mounts, rejected, cancellationToken).ConfigureAwait(false) : rejected;
        }

        var added = await _roster.AddAsync(manifest.Id, () => new WasmPluginRuntime(manifest, source.Directory, modulePath!, source.Permissions, mounts, _context, _loggerFactory.CreateLogger<WasmPluginRuntime>()), cancellationToken).ConfigureAwait(false);
        if (!added.TryGetValue(out var runtime))
        {
            return added.Error!;
        }

        var discovered = await runtime.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        return discovered.IsSuccess ? runtime : discovered.Error!;
    }

    /// <summary>
    /// 实例化任何模块之前的检查：权限、入口与模块文件
    /// </summary>
    /// <remarks>
    /// 通过时返回空并给出模块文件的绝对路径
    /// </remarks>
    private static Error? Preflight(PluginSource source, Manifest manifest, IReadOnlyDictionary<string, string> mounts, out string? modulePath)
    {
        modulePath = null;
        // Wasm 插件的权限按种类判定（capability 看授予、fs 看映射）；授予与入口的核对两个宿主相同
        var checkedEntry = PluginPreflight.Check(source, manifest, permission => IsGranted(permission, source.Permissions, mounts));
        if (!checkedEntry.TryGetValue(out var entry))
        {
            return checkedEntry.Error;
        }

        modulePath = Path.GetFullPath(Path.Combine(source.Directory, entry));
        return File.Exists(modulePath) ? null : PluginPreflight.EntryMissing(modulePath);
    }

    /// <summary>
    /// 以发现时被拒登记：不建实例，插件停在 Failed（<c>lifecycle.md</c> §1）
    /// </summary>
    /// <remarks>
    /// 返回拒绝的原因；同一清单 ID 已登记时返回冲突
    /// </remarks>
    private async Task<Result<WasmPluginRuntime>> RejectAsync(PluginSource source, Manifest manifest, IReadOnlyDictionary<string, string> mounts, Error error, CancellationToken cancellationToken)
    {
        var added = await _roster.AddAsync(
            manifest.Id,
            () =>
            {
                var runtime = new WasmPluginRuntime(manifest, source.Directory, string.Empty, source.Permissions, mounts, _context, _loggerFactory.CreateLogger<WasmPluginRuntime>());
                runtime.Reject(error);
                return runtime;
            },
            cancellationToken).ConfigureAwait(false);
        return added.IsSuccess ? error : added.Error!;
    }

    /// <summary>
    /// 一项清单权限是否被授予
    /// </summary>
    /// <remarks>
    /// <c>capability:</c> 看宿主授予，<c>fs.read:</c> / <c>fs.write:</c> 看是否映射，其余（含 <c>net.outbound:</c>）不授予
    /// </remarks>
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
    /// 把运行时清单贡献的中间件成组登记进管道
    /// </summary>
    /// <remarks>
    /// 由名册在登记锁内、目录、权限码与代理操作登记成功之后调用。没有贡献中间件时什么都不做；登记被拒时管道不变，返回内核的错误（<c>KERNEL_PIPELINE_003</c> / <c>KERNEL_PIPELINE_001</c>），名册随即撤回刚登记的目录、操作与权限码
    /// </remarks>
    private Result UseMiddleware(WasmPluginRuntime runtime)
    {
        if (_pipeline is null || runtime.Manifest.Contributes.Pipeline.Count == 0)
        {
            return Result.Success();
        }

        try
        {
            // 成组登记并立即排序核对：运行期没有「之后构建管道时再报」的机会，约束不成立就整组不进
            _pipeline.UseOwned(runtime.Manifest.Id, pipeline => AddMiddleware(pipeline, runtime));
            return Result.Success();
        }
        catch (FrameworkException exception)
        {
            return exception.ToError();
        }
    }

    /// <summary>
    /// 把运行时贡献的中间件按归属撤出管道
    /// </summary>
    /// <remarks>
    /// 卸载时由名册在加载卸载锁与登记锁内、把运行时移出名册之前调用。别的中间件以次序约束引用了它的中间件时撤不了（<c>KERNEL_PIPELINE_003</c>），这时管道不变，名册什么都不改、运行时照常服务
    /// </remarks>
    private Result RemoveMiddleware(WasmPluginRuntime runtime)
    {
        if (_pipeline is null)
        {
            return Result.Success();
        }

        try
        {
            _pipeline.RemoveOwnedBy(runtime.Manifest.Id);
            return Result.Success();
        }
        catch (FrameworkException exception)
        {
            return exception.ToError();
        }
    }

    /// <summary>
    /// 记录发现阶段失败的插件（宿主继续运行）
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Wasm plugin {Plugin} failed during discovery: {Code} {Message}")]
    private static partial void LogDiscoveryFailed(ILogger logger, string plugin, string code, string message);

    /// <summary>
    /// 记录插件启动失败（宿主继续运行）
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Wasm plugin {PluginId} failed to start: {Code} {Message}")]
    private static partial void LogStartFailed(ILogger logger, string pluginId, string code, string message);
}
