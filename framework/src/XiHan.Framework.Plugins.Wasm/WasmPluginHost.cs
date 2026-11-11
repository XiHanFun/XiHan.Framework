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
using XiHan.Framework.Kernel.Pipeline;
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
    public const string HealthPrefix = "plugins.";

    /// <summary>
    /// 选项
    /// </summary>
    private readonly WasmPluginOptions _options;

    /// <summary>
    /// 已登记的运行时，按清单 ID
    /// </summary>
    private readonly ConcurrentDictionary<string, WasmPluginRuntime> _plugins = new(StringComparer.Ordinal);

    /// <summary>
    /// 登记顺序（停止按相反顺序）
    /// </summary>
    private readonly List<WasmPluginRuntime> _order = [];

    /// <summary>
    /// 清单读不出来的插件目录与原因
    /// </summary>
    /// <remarks>
    /// 没有清单 ID，只作为健康条目 <c>plugins.&lt;目录名&gt;</c> 上报；只在配置阶段写入
    /// </remarks>
    private readonly List<(string Directory, Error Error)> _unreadable = [];

    /// <summary>
    /// 串行化加载与卸载
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 串行化代理操作与中间件的登记与撤回
    /// </summary>
    /// <remarks>
    /// 运行期加载的登记不持有 <see cref="_gate"/>，撤回发生在卸载时；两者互斥，登记前核对运行时仍是宿主里的当前运行时，与卸载交错的加载不会留下指向已卸载插件的登记或中间件（ADR-0085、ADR-0088）
    /// </remarks>
    private readonly Lock _registration = new();

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
    /// 代理操作登记到的登记表
    /// </summary>
    /// <remarks>
    /// 由特性在配置阶段提供
    /// </remarks>
    private OperationRegistry? _registry;

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
    public IReadOnlyList<PluginStatus> Plugins => _plugins.Values.Select(static r => r.Status).OrderBy(static s => s.Manifest.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 查找一个插件的诊断快照
    /// </summary>
    /// <remarks>
    /// 未加载时无值
    /// </remarks>
    public Maybe<WasmPluginStatus> FindStatus(string manifestId)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        return _plugins.TryGetValue(manifestId, out var runtime) ? runtime.Describe() : Maybe.None<WasmPluginStatus>();
    }

    /// <summary>
    /// 全部运行时（特性登记代理操作、订阅与中间件时使用）
    /// </summary>
    internal IReadOnlyList<WasmPluginRuntime> Runtimes => _order.ToList();

    /// <summary>
    /// 指定代理操作的登记表
    /// </summary>
    internal void UseRegistry(OperationRegistry registry) => _registry = registry;

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
    /// 代理操作、声明的权限码与贡献的中间件一次登记、全进或全不进（ADR-0085、ADR-0088）：中间件在 <see cref="PipelineBuilder.UseOwned"/> 里成组登记并立即核对，约束目标未登记、有歧义、跨阶段（<c>KERNEL_PIPELINE_003</c>）或成环（<c>KERNEL_PIPELINE_001</c>）时整个插件登记失败、已登记的操作与权限码撤回、管道不变。登记成功之后开始的调用经过插件的中间件
    /// </remarks>
    public async Task<Result<PluginStatus>> LoadAsync(PluginSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var manifest = ReadManifest(source.Directory);
        if (!manifest.TryGetValue(out var read))
        {
            return manifest.Error!;
        }

        var discovered = await DiscoverAsync(source, read, _options.MountsFor(read.Id), keepRejected: false, cancellationToken).ConfigureAwait(false);
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
    /// <remarks>
    /// 先按归属（清单 ID）撤回插件贡献的中间件、登记的代理操作与权限码，再停止并丢弃实例（<c>plugin/invocation.md</c> §1、<c>plugin/wasm.md</c> §6，ADR-0085、ADR-0088）：撤回之后开始的调用不再经过它的中间件，按 ID 调用它的操作返回 <c>KERNEL_NOTFOUND_001</c>；撤回之前已经进入管道的调用照常完成，或在插件停止后以 <c>PLUGINS_UNAVAILABLE_001</c> 结束。卸载完成后可以直接 <see cref="LoadAsync"/> 同一个插件。别的中间件以次序约束引用了它的中间件时卸载被拒（<c>KERNEL_PIPELINE_003</c>），什么都不撤、插件照常服务。订阅与消息目录在配置阶段接进容器，不随卸载撤回
    /// </remarks>
    public async Task<Result> UnloadAsync(string manifestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        WasmPluginRuntime? runtime;
        try
        {
            if (!_plugins.TryGetValue(manifestId, out runtime))
            {
                return Error.NotFound(PluginErrors.NotFound, PluginMessages.NotLoaded, MessageArguments.Of(("id", manifestId)));
            }

            // 撤回与移出在同一段 _gate 里：同一插件的再加载（DiscoverAsync 同样先取 _gate）一定看到撤回已经完成；
            // 先撤回再停止实例，新的调用立刻不再经过它的中间件、得到操作未登记
            var withdrawn = Withdraw(runtime);
            if (withdrawn.IsFailure)
            {
                return withdrawn.Error!;
            }
        }
        finally
        {
            _gate.Release();
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
        _loggerFactory = services.GetRequiredService<ILoggerFactory>();
        _logger = _loggerFactory.CreateLogger<WasmPluginHost>();
        foreach (var runtime in _order)
        {
            runtime.Logger = _loggerFactory.CreateLogger<WasmPluginRuntime>();
        }

        _context.Services = services;
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
        foreach (var runtime in Enumerable.Reverse(_order.ToList()))
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
    internal async ValueTask<Result> DeliverAsync(MessageDelivery delivery, InvocationContext context)
    {
        // 只投给以投递的方式订阅了该主题的插件：同一主题的竞争投递与广播投递各找各的插件
        foreach (var runtime in _order.Where(r => r.IsSubscribedTo(delivery.Topic, delivery.Envelope.Type, delivery.Mode)))
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
        => _order.Select(static r => new ContributorStatus(HealthPrefix + r.Manifest.Id, r.State) { Error = r.Error })
            .Concat(_unreadable.Select(static u => new ContributorStatus(HealthPrefix + Path.GetFileName(u.Directory), LifecycleState.Failed)
            {
                Error = u.Error,
            }))
            .ToList();

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
        foreach (var runtime in _order.ToList())
        {
            await runtime.DrainAsync().ConfigureAwait(false);
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
        var file = ReadManifestFile(full);
        if (!file.TryGetValue(out var read))
        {
            // 没有清单 ID：只能按目录名上报
            _unreadable.Add((full, file.Error!));
            return file.Error!;
        }

        var source = new PluginSource(full, _options.GrantsFor(read.Id));
        var mounts = _options.MountsFor(read.Id);
        var kind = CheckKind(read);
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

        WasmPluginRuntime runtime;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_plugins.ContainsKey(manifest.Id))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, PluginMessages.AlreadyLoaded, MessageArguments.Of(("id", manifest.Id)));
            }

            runtime = new WasmPluginRuntime(manifest, source.Directory, modulePath!, source.Permissions, mounts, _context, _loggerFactory.CreateLogger<WasmPluginRuntime>());
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
    /// 实例化任何模块之前的检查：权限、入口与模块文件
    /// </summary>
    /// <remarks>
    /// 通过时返回空并给出模块文件的绝对路径
    /// </remarks>
    private static Error? Preflight(PluginSource source, Manifest manifest, IReadOnlyDictionary<string, string> mounts, out string? modulePath)
    {
        modulePath = null;
        var missing = manifest.Permissions.Where(permission => !IsGranted(permission, source.Permissions, mounts)).ToList();
        if (missing.Count > 0)
        {
            return Error.Denied(PluginErrors.PermissionDenied, PluginMessages.PermissionsNotGranted, MessageArguments.Of(("id", manifest.Id), ("permissions", string.Join(", ", missing))));
        }

        if (string.IsNullOrWhiteSpace(manifest.Entry))
        {
            return new Error(PluginErrors.InvalidSource, PluginMessages.EntryRequired, MessageArguments.Of(("id", manifest.Id)));
        }

        modulePath = Path.GetFullPath(Path.Combine(source.Directory, manifest.Entry));
        return File.Exists(modulePath) ? null : Error.NotFound(PluginErrors.NotFound, PluginMessages.ExecutableMissing, MessageArguments.Of(("path", modulePath)));
    }

    /// <summary>
    /// 以发现时被拒登记：不建实例，插件停在 Failed（<c>lifecycle.md</c> §1）
    /// </summary>
    /// <remarks>
    /// 返回拒绝的原因；同一清单 ID 已登记时返回冲突
    /// </remarks>
    private async Task<Result<WasmPluginRuntime>> RejectAsync(PluginSource source, Manifest manifest, IReadOnlyDictionary<string, string> mounts, Error error, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_plugins.ContainsKey(manifest.Id))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, PluginMessages.AlreadyLoaded, MessageArguments.Of(("id", manifest.Id)));
            }

            var runtime = new WasmPluginRuntime(manifest, source.Directory, string.Empty, source.Permissions, mounts, _context, _loggerFactory.CreateLogger<WasmPluginRuntime>());
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
    /// 把运行时清单声明的权限码、握手声明的操作与清单贡献的中间件登记进宿主
    /// </summary>
    /// <remarks>
    /// 一组一次登记、全进或全不进：声明的权限码与已登记的重复、任一项与已有操作重名或引用了未登记的权限码，或中间件的约束目标未登记、有歧义、跨阶段、成环，插件整体登记失败，不会留下半组指向被拒插件的权限码、代理操作与中间件。运行时已不是宿主里的当前运行时（卸载与运行期加载交错）时不登记，返回未加载
    /// </remarks>
    private Result Register(WasmPluginRuntime runtime)
    {
        if (_registry is null || runtime.Handshake is null)
        {
            return Result.Success();
        }

        // 核对与登记在同一把锁里，与卸载的撤回互斥：先被撤回再登记进来的旧运行时会让同一插件的再加载撞上重名
        lock (_registration)
        {
            if (!_plugins.TryGetValue(runtime.Manifest.Id, out var current) || !ReferenceEquals(current, runtime))
            {
                return Error.NotFound(PluginErrors.NotFound, PluginMessages.NotLoaded, MessageArguments.Of(("id", runtime.Manifest.Id)));
            }

            var registered = PluginContributions.Register(_registry, runtime.Manifest, runtime.Handshake, runtime);
            if (registered.IsFailure)
            {
                return registered;
            }

            // 操作先进、中间件后进：中间件进不去时撤回操作与权限码不会失败，回滚总能完成
            var middleware = UseMiddleware(runtime);
            if (middleware.IsFailure)
            {
                PluginContributions.Withdraw(_registry, runtime.Manifest.Id);
            }

            return middleware;
        }
    }

    /// <summary>
    /// 把运行时清单贡献的中间件成组登记进管道
    /// </summary>
    /// <remarks>
    /// 在 <see cref="_registration"/> 内调用。没有贡献中间件时什么都不做；登记被拒时管道不变，返回内核的错误（<c>KERNEL_PIPELINE_003</c> / <c>KERNEL_PIPELINE_001</c>）
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
    /// 把运行时移出宿主，并撤回它贡献的中间件、登记的权限码与代理操作
    /// </summary>
    /// <remarks>
    /// 卸载时在 <see cref="_gate"/> 内调用。先撤中间件：别的中间件以次序约束引用了它的中间件时撤不了（<c>KERNEL_PIPELINE_003</c>），这时什么都不改、运行时留在宿主里照常服务。撤回与移出在同一段 <see cref="_registration"/> 里，与运行期加载的登记互斥
    /// </remarks>
    private Result Withdraw(WasmPluginRuntime runtime)
    {
        lock (_registration)
        {
            if (_pipeline is not null)
            {
                try
                {
                    _pipeline.RemoveOwnedBy(runtime.Manifest.Id);
                }
                catch (FrameworkException exception)
                {
                    return exception.ToError();
                }
            }

            _plugins.TryRemove(runtime.Manifest.Id, out _);
            _order.Remove(runtime);
            if (_registry is not null)
            {
                PluginContributions.Withdraw(_registry, runtime.Manifest.Id);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 从登记里移除
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
    /// 读目录里的清单并确认是 Wasm 插件
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
    /// 读目录里的清单文件
    /// </summary>
    private static Result<Manifest> ReadManifestFile(string directory)
    {
        var read = ManifestReader.ReadFile(Path.Combine(directory, ManifestReader.FileName));
        return read.TryGetValue(out var manifest)
            ? manifest
            : new Error(PluginErrors.InvalidSource, PluginMessages.ManifestInvalid, MessageArguments.Of(("directory", directory), ("reason", read.Error!.Message)), read.Error);
    }

    /// <summary>
    /// 清单是否描述一个 Wasm 插件
    /// </summary>
    private static Result CheckKind(Manifest manifest)
        => manifest.Kind == ManifestKind.Plugin && manifest.Isolation == PluginIsolation.Wasm
            ? Result.Success()
            : new Error(PluginErrors.InvalidSource, PluginMessages.NotOutOfProcess, MessageArguments.Of(("id", manifest.Id), ("kind", manifest.Kind.ToString()), ("isolation", manifest.Isolation?.ToString())));

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
