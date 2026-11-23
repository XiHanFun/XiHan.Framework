// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Plugins;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 一个宿主的插件名册：谁在宿主里、按什么顺序、登记了什么
/// </summary>
/// <remarks>
/// 进程外与 Wasm 宿主各持有一个（ADR-0260）。运行时进出名册、它们的目录、权限码与代理操作的登记与撤回、事件投递与健康条目都在这里；启动、停止与释放运行时是载体的事，留在宿主里。
/// 锁次序（ADR-0085、ADR-0088）：加载卸载锁 → 登记锁 → 登记表、管道与目录登记表各自的锁，只按这个方向取。运行期加载的登记与后台重试的补登记只取登记锁
/// </remarks>
/// <typeparam name="TRuntime">载体的运行时</typeparam>
internal sealed class PluginRoster<TRuntime> : IDisposable
    where TRuntime : class, IPluginRuntime
{
    /// <summary>
    /// 已登记的运行时，按清单 ID
    /// </summary>
    private readonly ConcurrentDictionary<string, TRuntime> _plugins = new(StringComparer.Ordinal);

    /// <summary>
    /// 登记顺序（停止按相反顺序）
    /// </summary>
    private readonly List<TRuntime> _order = [];

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
    /// 串行化目录、权限码、代理操作与载体追加项的登记与撤回
    /// </summary>
    /// <remarks>
    /// 登记发生在运行期加载与后台重试里（不持有 <see cref="_gate"/>），撤回发生在卸载时；两者互斥，登记前核对运行时仍是名册里的当前运行时，卸载之后才完成的握手、与卸载交错的加载都不会留下指向已卸载插件的登记（ADR-0085、ADR-0088）
    /// </remarks>
    private readonly Lock _registration = new();

    /// <summary>
    /// 代理操作登记到的登记表
    /// </summary>
    /// <remarks>
    /// 由特性在配置阶段提供
    /// </remarks>
    private OperationRegistry? _registry;

    /// <summary>
    /// 插件消息目录登记到的登记表
    /// </summary>
    /// <remarks>
    /// 宿主附着到容器时取得；配置阶段发现的插件的目录由特性按归属注册进容器，卸载时同样从这里撤回
    /// </remarks>
    private MessageCatalogRegistry? _catalogs;

    /// <summary>
    /// 每个插件的状态，按清单 ID 排序
    /// </summary>
    public IReadOnlyList<PluginStatus> Statuses => _plugins.Values.Select(static r => r.Status).OrderBy(static s => s.Manifest.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// 全部运行时，按登记顺序
    /// </summary>
    public IReadOnlyList<TRuntime> Runtimes => _order.ToList();

    /// <summary>
    /// 清单读不出来的插件目录与原因
    /// </summary>
    public IReadOnlyList<(string Directory, Error Error)> Unreadable => _unreadable;

    /// <summary>
    /// 指定代理操作的登记表
    /// </summary>
    public void UseRegistry(OperationRegistry registry) => _registry = registry;

    /// <summary>
    /// 指定插件消息目录的登记表
    /// </summary>
    public void UseCatalogs(MessageCatalogRegistry catalogs) => _catalogs = catalogs;

    /// <summary>
    /// 查找一个插件的运行时
    /// </summary>
    public bool TryFind(string manifestId, [NotNullWhen(true)] out TRuntime? runtime)
        => _plugins.TryGetValue(manifestId, out runtime);

    /// <summary>
    /// 记下一个清单读不出来的插件目录
    /// </summary>
    /// <remarks>
    /// 只在配置阶段调用
    /// </remarks>
    public void AddUnreadable(string directory, Error error) => _unreadable.Add((directory, error));

    /// <summary>
    /// 建一个运行时并放进名册
    /// </summary>
    /// <remarks>
    /// 同一清单 ID 已在名册里时不建，返回 <c>PLUGINS_CONFLICT_001</c>。发现时被拒的插件同样经这里进名册，由 <paramref name="create"/> 建好并标记为被拒
    /// </remarks>
    /// <param name="manifestId">清单 ID</param>
    /// <param name="create">建运行时；在加载卸载锁内调用</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<Result<TRuntime>> AddAsync(string manifestId, Func<TRuntime> create, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        ArgumentNullException.ThrowIfNull(create);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 核对与放入在同一段 _gate 里：同一插件的两次加载只有一次进得来，卸载的撤回也在这把锁里完成
            if (_plugins.ContainsKey(manifestId))
            {
                return Error.Conflict(PluginErrors.AlreadyLoaded, PluginMessages.AlreadyLoaded, MessageArguments.Of(("id", manifestId)));
            }

            var runtime = create();
            _plugins[manifestId] = runtime;
            _order.Add(runtime);
            return runtime;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 把握手声明的目录与操作、清单声明的权限码登记进宿主，再登记载体追加的部分
    /// </summary>
    /// <remarks>
    /// 一组一次登记、全进或全不进：目录与同名目录冲突、声明的权限码与已登记的重复、任一项与已有操作重名或引用了未登记的权限码，或 <paramref name="then"/> 失败（Wasm 的中间件约束不成立），插件整体登记失败，不会留下半组。运行时已不是名册里的当前运行时（卸载之后才握手成功的重试、卸载与运行期加载交错）时不登记，返回未加载。没有登记表或还没握手成功时什么都不做
    /// </remarks>
    /// <param name="runtime">运行时</param>
    /// <param name="then">目录、权限码与操作登记成功之后、仍在登记锁内做的追加登记；没有时为空</param>
    public Result Register(TRuntime runtime, Func<TRuntime, Result>? then)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (_registry is not { } registry || runtime.Handshake is not { } handshake)
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

            var registered = PluginContributions.Register(registry, _catalogs, runtime.Manifest, handshake, runtime);
            if (registered.IsFailure || then is null)
            {
                return registered;
            }

            // 操作先进、追加项后进：追加项进不去时撤回目录、操作与权限码不会失败，回滚总能完成
            var followed = then(runtime);
            if (followed.IsFailure)
            {
                PluginContributions.Withdraw(registry, _catalogs, runtime.Manifest.Id);
            }

            return followed;
        }
    }

    /// <summary>
    /// 把一个插件移出名册并撤回它登记的代理操作、权限码与目录
    /// </summary>
    /// <remarks>
    /// 卸载的第一步，返回移出的运行时，由宿主接着停止并释放。不在名册里返回 <c>PLUGINS_NOTFOUND_001</c>。<paramref name="first"/> 失败（Wasm 的中间件被别的中间件以次序约束引用）时什么都不改，运行时留在名册里照常服务。撤回的顺序：载体追加的部分、移出名册、代理操作与权限码、目录（ADR-0085、ADR-0088、ADR-0252）
    /// </remarks>
    /// <param name="manifestId">清单 ID</param>
    /// <param name="first">在登记锁内、移出名册之前先撤回的载体追加项；没有时为空</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<Result<TRuntime>> WithdrawAsync(string manifestId, Func<TRuntime, Result>? first, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_plugins.TryGetValue(manifestId, out var runtime))
            {
                return Error.NotFound(PluginErrors.NotFound, PluginMessages.NotLoaded, MessageArguments.Of(("id", manifestId)));
            }

            // 撤回与移出在同一段 _gate 里：同一插件的再加载（AddAsync 同样先取 _gate）一定看到撤回已经完成，不会撞上自己上一版留下的登记
            // 又在同一段 _registration 里，与运行期加载的登记互斥：登记要么整组在撤回之前、随即被撤回，要么在移出之后、核对不过
            lock (_registration)
            {
                if (first?.Invoke(runtime) is { IsFailure: true } refused)
                {
                    return refused.Error!;
                }

                _plugins.TryRemove(manifestId, out _);
                _order.Remove(runtime);
                if (_registry is { } registry)
                {
                    PluginContributions.Withdraw(registry, _catalogs, manifestId);
                }
            }

            return runtime;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 把运行时从名册里移除
    /// </summary>
    /// <remarks>
    /// 运行期加载登记不进去时用：它什么都没登记，不用撤回
    /// </remarks>
    public void Remove(TRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _plugins.TryRemove(runtime.Manifest.Id, out _);
        lock (_order)
        {
            _order.Remove(runtime);
        }
    }

    /// <summary>
    /// 把一条消息投递给订阅了该主题与类型的插件
    /// </summary>
    /// <remarks>
    /// 按登记顺序逐个投递，第一个失败即为结果
    /// </remarks>
    public async ValueTask<Result> DeliverAsync(MessageDelivery delivery, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        // 只投给以投递的方式订阅了该主题的插件：同一主题的竞争投递与广播投递各找各的插件
        foreach (var runtime in _order.Where(r => IsSubscribed(r, delivery)))
        {
            var delivered = await runtime.DeliverAsync(delivery, context).ConfigureAwait(false);
            if (delivered.IsFailure)
            {
                return delivered;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// 每个插件一条健康条目，清单读不出来的目录按目录名各一条
    /// </summary>
    public IReadOnlyList<ContributorStatus> Check()
        => _order.Select(static r => new ContributorStatus(PluginHealth.EntryPrefix + r.Manifest.Id, r.State) { Error = r.Error })
            .Concat(_unreadable.Select(static u => new ContributorStatus(PluginHealth.EntryPrefix + Path.GetFileName(u.Directory), LifecycleState.Failed)
            {
                Error = u.Error,
            }))
            .ToList();

    /// <summary>
    /// 释放加载卸载锁
    /// </summary>
    /// <remarks>
    /// 运行时由宿主停止并释放，不在这里
    /// </remarks>
    public void Dispose() => _gate.Dispose();

    /// <summary>
    /// 运行时是否以投递的方式订阅了它的主题与类型
    /// </summary>
    private static bool IsSubscribed(TRuntime runtime, MessageDelivery delivery)
        => runtime.Handshake?.Subscriptions.Any(s => s.Matches(delivery.Topic, delivery.Envelope.Type, delivery.Mode)) == true;
}
