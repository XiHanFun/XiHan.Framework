// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 按固定阶段组装执行管道（RFC-0004）
/// </summary>
/// <remarks>
/// 特性（<c>FeatureContext.AddMiddleware</c>）与应用（<c>AppBuilder.ConfigurePipeline</c>）在阶段内登记中间件并声明相对约束，构建时得到确定、可打印的顺序。按类型登记的中间件由 <c>AppBuilder.Build</c> 以单例补登记进容器；不经 <c>AppBuilder</c> 直接使用时由调用方注册。应用构建之后只能经 <see cref="UseOwned"/> 成组登记、经 <see cref="RemoveOwnedBy"/> 按归属撤回（RFC-0004「运行期改动」）；全部成员线程安全
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PipelineBuilder
{
    /// <summary>
    /// 应用级注册的归属名
    /// </summary>
    public const string AppOwner = "app";

    /// <summary>
    /// 全部注册，按注册顺序
    /// </summary>
    private readonly List<Registration> _registrations = [];

    /// <summary>
    /// 按（类型，名字）索引的注册，用于去重
    /// </summary>
    /// <remarks>
    /// 类型的约束目标经 <see cref="FindByType"/> 解析
    /// </remarks>
    private readonly Dictionary<MiddlewareKey, Registration> _byKey = [];

    /// <summary>
    /// 串行化登记、撤回、排序与构建
    /// </summary>
    /// <remarks>
    /// 配置阶段只有装配线程在登记；应用构建之后，插件宿主可能在运行期成组登记或按归属撤回，与并发调用取管道快照（<see cref="Build(PipelineStep, out long)"/>）、打印互斥。可重入：<see cref="UseOwned"/> 持锁期间回调里的登记再次进入
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>
    /// 下一个注册序号
    /// </summary>
    /// <remarks>
    /// 单调递增、不随撤回回退：按归属撤回之后再登记的中间件不会与留下的注册重号
    /// </remarks>
    private int _nextSequence;

    /// <summary>
    /// 管道版本
    /// </summary>
    /// <remarks>
    /// 每次改动加一，在 <see cref="_gate"/> 内写、无锁读；应用据此判断已构建的委托是否过时
    /// </remarks>
    private long _version;

    /// <summary>
    /// 应用是否已构建
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterServices"/> 之后为真：容器已建好、按类型登记的中间件进不了容器，管道只能经 <see cref="UseOwned"/> 按实例成组登记、经 <see cref="RemoveOwnedBy"/> 撤回
    /// </remarks>
    private bool _sealed;

    /// <summary>
    /// 正在成组登记的归属
    /// </summary>
    /// <remarks>
    /// 只在 <see cref="UseOwned"/> 持锁期间非空：组内的登记与次序约束都必须属于它，撤回时才能一个不漏、一个不多
    /// </remarks>
    private string? _group;

    /// <summary>
    /// 管道版本
    /// </summary>
    /// <remarks>
    /// 每次登记、约束、成组登记与撤回都加一；<c>App</c> 版本没变时复用已构建的委托
    /// </remarks>
    internal long Version => Volatile.Read(ref _version);

    /// <summary>
    /// 在 <see cref="PipelineStage.Transaction"/> 阶段末尾添加中间件
    /// </summary>
    /// <remarks>
    /// 等价于 <c>UseAt&lt;T&gt;(PipelineStage.Transaction)</c>
    /// </remarks>
    public PipelineBuilder Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : class, IPipelineMiddleware
        => UseAt<TMiddleware>(PipelineStage.Transaction);

    /// <summary>
    /// 在指定阶段添加从调用作用域解析的中间件
    /// </summary>
    /// <remarks>
    /// 构建应用时内核以单例把中间件类型补登记进容器，应用已有的登记不覆盖（RFC-0004「登记入口」）
    /// </remarks>
    public PipelineBuilder UseAt<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>(PipelineStage stage, Placement placement = Placement.End, string owner = AppOwner)
        where TMiddleware : class, IPipelineMiddleware
        => Add(typeof(TMiddleware), null, stage, placement, owner, static services => services.GetRequiredService<TMiddleware>(), static services => services.TryAddSingleton<TMiddleware>());

    /// <summary>
    /// 在指定阶段添加一个已创建的中间件实例
    /// </summary>
    public PipelineBuilder UseInstance<TMiddleware>(TMiddleware instance, PipelineStage stage, Placement placement = Placement.End, string owner = AppOwner)
        where TMiddleware : class, IPipelineMiddleware
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Add(typeof(TMiddleware), null, stage, placement, owner, _ => instance, register: null);
    }

    /// <summary>
    /// 在指定阶段添加一个带名字的中间件实例
    /// </summary>
    /// <remarks>
    /// 同一类型可按不同名字登记多次（如插件贡献的多个中间件），名字在同类型内唯一。按类型声明的相对约束（<see cref="Before{TMiddleware, TOther}"/> / <see cref="After{TMiddleware, TOther}"/>）在该类型有多个实例时不可用，改用按名字的 <see cref="Before(string, string)"/> / <see cref="After(string, string)"/>
    /// </remarks>
    public PipelineBuilder UseNamedInstance(string name, IPipelineMiddleware instance, PipelineStage stage, Placement placement = Placement.End, string owner = AppOwner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(instance);
        return Add(instance.GetType(), name, stage, placement, owner, _ => instance, register: null);
    }

    /// <summary>
    /// 约束 <typeparamref name="TMiddleware"/> 位于 <typeparamref name="TOther"/> 之前
    /// </summary>
    /// <remarks>
    /// 两者必须在同一阶段。跨包依赖必须这样声明：阶段内的基准次序（归属清单 ID 字典序）只保证可复现，不表达依赖
    /// </remarks>
    public PipelineBuilder Before<TMiddleware, TOther>()
        where TMiddleware : class, IPipelineMiddleware
        where TOther : class, IPipelineMiddleware
    {
        lock (_gate)
        {
            var registration = Constrain(Require(typeof(TMiddleware)));
            registration.Before.Add(typeof(TOther));
            Touch();
        }

        return this;
    }

    /// <summary>
    /// 约束 <typeparamref name="TMiddleware"/> 位于 <typeparamref name="TOther"/> 之后
    /// </summary>
    /// <remarks>
    /// 两者必须在同一阶段。要用到另一个包中间件产出（认证后的主体、解析后的租户）的中间件必须这样声明，基准次序不表达依赖
    /// </remarks>
    public PipelineBuilder After<TMiddleware, TOther>()
        where TMiddleware : class, IPipelineMiddleware
        where TOther : class, IPipelineMiddleware
    {
        lock (_gate)
        {
            var registration = Constrain(Require(typeof(TMiddleware)));
            registration.After.Add(typeof(TOther));
            Touch();
        }

        return this;
    }

    /// <summary>
    /// 按名字约束一个中间件位于另一个之前
    /// </summary>
    /// <remarks>
    /// 名字是按名字登记的实例名，或只登记一次、没有名字的中间件的类型全名（RFC-0004 第 7 条）；同类型有多个命名实例时用它代替 <see cref="Before{TMiddleware, TOther}"/>。<paramref name="middleware"/> 必须已登记；<paramref name="other"/> 在排序时解析，未登记、有歧义或不在同一阶段抛出 <see cref="KernelErrors.PipelineRegistration"/>
    /// </remarks>
    /// <param name="middleware">要排在前面的中间件名</param>
    /// <param name="other">要排在后面的中间件名</param>
    public PipelineBuilder Before(string middleware, string other)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(other);
        lock (_gate)
        {
            Constrain(RequireByName(middleware)).BeforeNames.Add(other);
            Touch();
        }

        return this;
    }

    /// <summary>
    /// 按名字约束一个中间件位于另一个之后
    /// </summary>
    /// <remarks>
    /// 名字的含义与解析同 <see cref="Before(string, string)"/>
    /// </remarks>
    /// <param name="middleware">要排在后面的中间件名</param>
    /// <param name="other">要排在前面的中间件名</param>
    public PipelineBuilder After(string middleware, string other)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(other);
        lock (_gate)
        {
            Constrain(RequireByName(middleware)).AfterNames.Add(other);
            Touch();
        }

        return this;
    }

    /// <summary>
    /// 一次登记同一归属的一组中间件与它们的次序约束，全进或全不进
    /// </summary>
    /// <remarks>
    /// <para><paramref name="configure"/> 在构建器的锁里执行，只应调用本构建器的登记方法：组内登记的归属都必须是 <paramref name="owner"/>，次序约束只能声明在组内的中间件上（目标可以在组外），否则抛出 <see cref="KernelErrors.PipelineRegistration"/>——这样 <see cref="RemoveOwnedBy"/> 撤回的恰好是这一组</para>
    /// <para>回调返回后立即按排序规则核对整条管道，约束目标未登记、有歧义、跨阶段（<see cref="KernelErrors.PipelineRegistration"/>）或成环（<see cref="KernelErrors.PipelineCycle"/>）时撤掉组内的全部登记再抛出，管道不变；不像配置阶段的单项登记那样等到构建或打印管道时才报。应用构建之后（容器已建好）组内只能按实例登记（<see cref="UseInstance"/>、<see cref="UseNamedInstance"/>）。成功后管道版本前进：之后开始的调用经过新登记的中间件，已经取到管道委托、正在执行的调用不受影响。不能嵌套</para>
    /// </remarks>
    /// <param name="owner">这一组的归属（插件或特性的清单 ID）</param>
    /// <param name="configure">在构建器上登记这一组的回调</param>
    public PipelineBuilder UseOwned(string owner, Action<PipelineBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(configure);
        lock (_gate)
        {
            if (_group is not null)
            {
                throw new FrameworkException(KernelErrors.PipelineRegistration, $"A middleware group owned by '{_group}' is being registered; groups cannot be nested.");
            }

            var start = _registrations.Count;
            _group = owner;
            try
            {
                configure(this);
                // 组内约束的目标可能在组外：此刻排序一次核对整条管道，不把错误留给之后的调用在构建时才撞上
                _ = Order();
            }
            catch
            {
                // 组内的登记都追加在末尾，约束只挂在组内的登记上：截掉末尾即恢复到成组登记之前
                Restore(_registrations.Take(start).ToList());
                throw;
            }
            finally
            {
                _group = null;
            }
        }

        return this;
    }

    /// <summary>
    /// 按归属撤回登记的全部中间件
    /// </summary>
    /// <remarks>
    /// 在构建器的锁里一次撤回该归属的全部中间件（连同挂在它们上面的次序约束），之后立即按排序规则核对剩下的管道：别的归属的中间件以次序约束引用了被撤回的中间件时，撤回会留下指向不存在的中间件的约束，这时什么都不撤、抛出 <see cref="KernelErrors.PipelineRegistration"/>，先撤回引用方。没有该归属的登记时什么都不做、返回 0。成功后管道版本前进：之后开始的调用不再经过被撤回的中间件，已经取到管道委托、正在执行的调用仍按原来的管道走完。不能在 <see cref="UseOwned"/> 的回调里调用
    /// </remarks>
    /// <param name="owner">归属（插件或特性的清单 ID）</param>
    /// <returns>撤回的中间件数</returns>
    public int RemoveOwnedBy(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        lock (_gate)
        {
            if (_group is not null)
            {
                throw new FrameworkException(KernelErrors.PipelineRegistration, $"A middleware group owned by '{_group}' is being registered; middleware cannot be removed meanwhile.");
            }

            var kept = _registrations.Where(r => !string.Equals(r.Owner, owner, StringComparison.Ordinal)).ToList();
            var removed = _registrations.Count - kept.Count;
            if (removed == 0)
            {
                return 0;
            }

            var original = _registrations.ToList();
            Restore(kept);
            try
            {
                // 撤回不能留下悬空的约束：剩下的管道排不出来就整体还原，插件照常服务，由调用方先撤回引用方
                _ = Order();
            }
            catch
            {
                Restore(original);
                throw;
            }

            return removed;
        }
    }

    /// <summary>
    /// 计算最终顺序
    /// </summary>
    /// <remarks>
    /// 约束成环抛出 <see cref="KernelErrors.PipelineCycle"/>，约束引用未注册或跨阶段的中间件抛出 <see cref="KernelErrors.PipelineRegistration"/>
    /// </remarks>
    public IReadOnlyList<MiddlewareDescriptor> Describe()
    {
        lock (_gate)
        {
            return Order().Select(static r => r.Descriptor).ToList();
        }
    }

    /// <summary>
    /// 按 RFC-0004 的格式打印最终顺序，一行一个中间件
    /// </summary>
    /// <remarks>
    /// 带名字的实例在类型名后以 <c>[名字]</c> 标出
    /// </remarks>
    public string Dump()
    {
        var builder = new StringBuilder();
        foreach (var descriptor in Describe())
        {
            builder.Append(descriptor.Stage.ToManifestName().PadRight(13)).Append(descriptor.Middleware.FullName);
            if (descriptor.Name is { } name)
            {
                builder.Append('[').Append(name).Append(']');
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// 把按类型登记的中间件以单例补登记进容器
    /// </summary>
    /// <remarks>
    /// 由 <c>AppBuilder.Build</c> 在全部特性装配之后调用；已有的登记（含应用自己的 Scoped 登记）不覆盖，按实例登记的不进容器
    /// </remarks>
    internal void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        lock (_gate)
        {
            foreach (var registration in _registrations)
            {
                registration.Register?.Invoke(services);
            }

            // 容器随后建好：之后按类型登记的中间件进不了容器，单项登记也不再核对就生效，统统改走成组登记
            _sealed = true;
        }
    }

    /// <summary>
    /// 构建管道委托
    /// </summary>
    /// <remarks>
    /// 每次调用时中间件从 <see cref="InvocationContext.Services"/> 解析；<paramref name="terminal"/> 在所有中间件之后执行
    /// </remarks>
    public PipelineStep Build(PipelineStep terminal) => Build(terminal, out _);

    /// <summary>
    /// 构建管道委托并给出它对应的管道版本
    /// </summary>
    /// <remarks>
    /// 排序与取版本在同一段锁里：委托恰好是该版本的管道，之后的改动使版本前进。<c>App</c> 据此缓存委托
    /// </remarks>
    /// <param name="terminal">所有中间件之后执行的终点</param>
    /// <param name="version">委托对应的管道版本</param>
    internal PipelineStep Build(PipelineStep terminal, out long version)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        List<Registration> ordered;
        lock (_gate)
        {
            ordered = Order();
            version = _version;
        }

        // 从终点向前逐层包裹：每一层捕获它的下一步，最终返回最外层（第一个中间件）的委托
        var next = terminal;
        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            var resolve = ordered[i].Resolve;
            var captured = next;
            next = context => resolve(context.Services).InvokeAsync(context, captured);
        }

        return next;
    }

    /// <summary>
    /// 登记一个中间件
    /// </summary>
    /// <remarks>
    /// 校验阶段合法、（类型，名字）未重复，并记录注册序号作为最终排序的决胜键
    /// </remarks>
    /// <param name="middleware">中间件类型</param>
    /// <param name="name">实例名字；按类型登记为空</param>
    /// <param name="stage">所属阶段</param>
    /// <param name="placement">阶段内位置</param>
    /// <param name="owner">注册者</param>
    /// <param name="resolve">每次调用时取得中间件实例的方法</param>
    /// <param name="register">把中间件类型登记进容器的方法；按实例登记为空</param>
    private PipelineBuilder Add(Type middleware, string? name, PipelineStage stage, Placement placement, string owner, Func<IServiceProvider, IPipelineMiddleware> resolve, Action<IServiceCollection>? register)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (!Enum.IsDefined(stage))
        {
            throw new FrameworkException(KernelErrors.UnknownStage, $"Unknown pipeline stage '{stage}'.");
        }

        lock (_gate)
        {
            EnsureMutable();
            var label = name is null ? middleware.FullName : $"{middleware.FullName}[{name}]";
            if (_group is not null && !string.Equals(owner, _group, StringComparison.Ordinal))
            {
                throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{label}' is owned by '{owner}' but is registered in the group owned by '{_group}'; a group has exactly one owner.");
            }

            // 容器已经建好：按类型登记的中间件补登记不进去，每次调用解析都会失败，现在就拒绝
            if (_sealed && register is not null)
            {
                throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{label}' cannot be registered by type after the application has been built; register an instance with 'UseInstance' or 'UseNamedInstance'.");
            }

            var key = new MiddlewareKey(middleware, name);
            if (_byKey.ContainsKey(key))
            {
                throw new FrameworkException(KernelErrors.PipelineRegistration, name is null
                    ? $"Middleware '{middleware.FullName}' is already registered; each middleware type may appear only once in a pipeline unless its instances are named."
                    : $"Middleware '{middleware.FullName}' named '{name}' is already registered; instance names must be unique within a type.");
            }

            var registration = new Registration(middleware, name, stage, placement, owner, _nextSequence++, resolve, register);
            _registrations.Add(registration);
            _byKey[key] = registration;
            Touch();
        }

        return this;
    }

    /// <summary>
    /// 应用构建之后拒绝单项改动
    /// </summary>
    /// <remarks>
    /// 单项登记与约束不立即核对（目标可以晚于约束登记），应用构建之后这样改会让下一次调用在构建管道时才失败；成组登记（<see cref="UseOwned"/>）期间放行，由它在回调之后整体核对
    /// </remarks>
    private void EnsureMutable()
    {
        if (_sealed && _group is null)
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, "The pipeline of a built application can only be changed with 'UseOwned' or 'RemoveOwnedBy'.");
        }
    }

    /// <summary>
    /// 核对能否在约束主体上声明次序约束
    /// </summary>
    /// <remarks>
    /// 应用构建之后只能在成组登记里声明；成组登记时主体必须是组内的中间件：挂在组外中间件上的约束不会随这一组撤回
    /// </remarks>
    /// <param name="registration">约束主体</param>
    private Registration Constrain(Registration registration)
    {
        EnsureMutable();
        if (_group is not null && !string.Equals(registration.Owner, _group, StringComparison.Ordinal))
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{registration.Label}' is not in the group owned by '{_group}'; a group can only declare ordering constraints for its own middleware.");
        }

        return registration;
    }

    /// <summary>
    /// 管道版本前进
    /// </summary>
    /// <remarks>
    /// 在 <see cref="_gate"/> 内调用；读取方无锁读取
    /// </remarks>
    private void Touch() => Volatile.Write(ref _version, _version + 1);

    /// <summary>
    /// 把注册换成给定的列表并重建索引
    /// </summary>
    /// <remarks>
    /// 在 <see cref="_gate"/> 内调用，撤回与回滚共用；注册对象本身（含挂在上面的约束）不变
    /// </remarks>
    /// <param name="registrations">新的注册列表，按注册顺序</param>
    private void Restore(List<Registration> registrations)
    {
        _registrations.Clear();
        _registrations.AddRange(registrations);
        _byKey.Clear();
        foreach (var registration in registrations)
        {
            _byKey[new MiddlewareKey(registration.Middleware, registration.Name)] = registration;
        }

        Touch();
    }

    /// <summary>
    /// 取按类型唯一确定的注册
    /// </summary>
    /// <remarks>
    /// 未注册时抛出 <see cref="KernelErrors.PipelineRegistration"/>。约束必须在注册之后声明
    /// </remarks>
    private Registration Require(Type middleware)
        => FindByType(middleware) ?? throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{middleware.FullName}' is not registered, so no ordering constraint can be declared for it.");

    /// <summary>
    /// 按类型找注册
    /// </summary>
    /// <remarks>
    /// 恰好一个返回它；没有返回空；多个（按名字登记的多个实例）抛出，因为按类型的约束无法指向其中某一个
    /// </remarks>
    private Registration? FindByType(Type middleware)
    {
        Registration? found = null;
        foreach (var registration in _registrations)
        {
            if (registration.Middleware != middleware)
            {
                continue;
            }

            if (found is not null)
            {
                throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{middleware.FullName}' has several named instances, so ordering constraints by type are ambiguous for it.");
            }

            found = registration;
        }

        return found;
    }

    /// <summary>
    /// 取按名字唯一确定的注册
    /// </summary>
    /// <remarks>
    /// 未注册或有歧义时抛出 <see cref="KernelErrors.PipelineRegistration"/>。约束必须在注册之后声明
    /// </remarks>
    private Registration RequireByName(string middleware)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(middleware);
        return FindByName(middleware) ?? throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{middleware}' is not registered, so no ordering constraint can be declared for it.");
    }

    /// <summary>
    /// 按名字找注册
    /// </summary>
    /// <remarks>
    /// 名字匹配按名字登记的实例名，或没有名字的注册的类型全名。恰好一个返回它，没有返回空，多个（不同类型的实例同名，或实例名与某个类型全名相同）抛出
    /// </remarks>
    private Registration? FindByName(string middleware)
    {
        Registration? found = null;
        foreach (var registration in _registrations)
        {
            var matches = registration.Name is { } name
                ? string.Equals(name, middleware, StringComparison.Ordinal)
                : string.Equals(registration.Middleware.FullName, middleware, StringComparison.Ordinal);
            if (!matches)
            {
                continue;
            }

            if (found is not null)
            {
                throw new FrameworkException(KernelErrors.PipelineRegistration, $"The middleware name '{middleware}' matches several registrations, so ordering constraints by name are ambiguous for it.");
            }

            found = registration;
        }

        return found;
    }

    /// <summary>
    /// 计算全部中间件的最终顺序
    /// </summary>
    /// <remarks>
    /// 按阶段枚举顺序逐阶段处理，阶段内先按位置、归属、注册序号排出基准次序，再按相对约束调整
    /// </remarks>
    private List<Registration> Order()
    {
        var result = new List<Registration>(_registrations.Count);
        foreach (var stage in PipelineStageExtensions.All)
        {
            // 归属按序号比较而不是注册顺序：同阶段里不同包的中间件顺序与 UseFeature 的调用顺序无关，结果可复现
            var inStage = _registrations
                .Where(r => r.Stage == stage)
                .OrderBy(static r => r.Placement == Placement.Beginning ? 0 : 1)
                .ThenBy(static r => r.Owner, StringComparer.Ordinal)
                .ThenBy(static r => r.Sequence)
                .ToList();

            result.AddRange(SortByConstraints(inStage, stage));
        }

        return result;
    }

    /// <summary>
    /// 在一个阶段内按 Before/After 约束做稳定拓扑排序
    /// </summary>
    /// <remarks>
    /// 无约束时保持 <paramref name="candidates"/> 的次序，约束成环抛出 <see cref="KernelErrors.PipelineCycle"/>
    /// </remarks>
    /// <param name="candidates">该阶段的中间件，已按基准次序排好</param>
    /// <param name="stage">所属阶段，用于错误信息与跨阶段校验</param>
    private List<Registration> SortByConstraints(List<Registration> candidates, PipelineStage stage)
    {
        // 边：from 必须先于 to。入度为零的按原有次序出队，保证确定性；节点以注册对象为键，同类型的多个命名实例互不混淆
        var successors = candidates.ToDictionary(static r => r, static _ => new List<Registration>());
        var indegree = candidates.ToDictionary(static r => r, static _ => 0);

        foreach (var registration in candidates)
        {
            foreach (var later in registration.Before)
            {
                var other = ResolveConstraintTarget(registration, later, stage);
                successors[registration].Add(other);
                indegree[other]++;
            }

            foreach (var earlier in registration.After)
            {
                var other = ResolveConstraintTarget(registration, earlier, stage);
                successors[other].Add(registration);
                indegree[registration]++;
            }

            foreach (var later in registration.BeforeNames)
            {
                var other = ResolveNamedTarget(registration, later, stage);
                successors[registration].Add(other);
                indegree[other]++;
            }

            foreach (var earlier in registration.AfterNames)
            {
                var other = ResolveNamedTarget(registration, earlier, stage);
                successors[other].Add(registration);
                indegree[registration]++;
            }
        }

        var ordered = new List<Registration>(candidates.Count);
        var ready = new List<Registration>(candidates.Where(r => indegree[r] == 0));
        while (ready.Count > 0)
        {
            var current = ready[0];
            ready.RemoveAt(0);
            ordered.Add(current);
            foreach (var successor in successors[current])
            {
                if (--indegree[successor] == 0)
                {
                    // 新就绪的节点按基准次序插回队列，而不是追加到末尾，这样约束只改变必要的相对位置
                    var insertAt = ready.FindIndex(r => candidates.IndexOf(r) > candidates.IndexOf(successor));
                    ready.Insert(insertAt < 0 ? ready.Count : insertAt, successor);
                }
            }
        }

        // 有节点始终入度不为零，说明约束之间成环；把仍被卡住的节点列出来
        if (ordered.Count != candidates.Count)
        {
            var stuck = candidates.Where(r => indegree[r] > 0).Select(static r => $"'{r.Middleware.FullName}'");
            throw new FrameworkException(KernelErrors.PipelineCycle, $"Middleware ordering constraints in stage '{stage.ToManifestName()}' form a cycle: {string.Join(", ", stuck)}.");
        }

        return ordered;
    }

    /// <summary>
    /// 解析约束指向的中间件
    /// </summary>
    /// <remarks>
    /// 必须已注册、按类型唯一且与 <paramref name="source"/> 在同一阶段，否则抛出 <see cref="KernelErrors.PipelineRegistration"/>
    /// </remarks>
    /// <param name="source">声明约束的中间件</param>
    /// <param name="target">约束指向的中间件类型</param>
    /// <param name="stage">当前阶段</param>
    private Registration ResolveConstraintTarget(Registration source, Type target, PipelineStage stage)
    {
        var other = FindByType(target) ?? throw new FrameworkException(KernelErrors.PipelineRegistration, $"The ordering constraint of middleware '{source.Middleware.FullName}' references unregistered middleware '{target.FullName}'.");

        // 跨阶段的先后由阶段顺序决定，相对约束只在阶段内有意义
        if (other.Stage != stage)
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{source.Middleware.FullName}' (stage '{stage.ToManifestName()}') and '{target.FullName}' (stage '{other.Stage.ToManifestName()}') are not in the same stage, so no relative order can be declared between them.");
        }

        return other;
    }

    /// <summary>
    /// 解析按名字的约束指向的中间件
    /// </summary>
    /// <remarks>
    /// 必须已注册、名字唯一且与 <paramref name="source"/> 在同一阶段，否则抛出 <see cref="KernelErrors.PipelineRegistration"/>
    /// </remarks>
    /// <param name="source">声明约束的中间件</param>
    /// <param name="target">约束指向的中间件名</param>
    /// <param name="stage">当前阶段</param>
    private Registration ResolveNamedTarget(Registration source, string target, PipelineStage stage)
    {
        var other = FindByName(target) ?? throw new FrameworkException(KernelErrors.PipelineRegistration, $"The ordering constraint of middleware '{source.Label}' references unregistered middleware '{target}'.");
        if (other.Stage != stage)
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{source.Label}' (stage '{stage.ToManifestName()}') and '{target}' (stage '{other.Stage.ToManifestName()}') are not in the same stage, so no relative order can be declared between them.");
        }

        return other;
    }

    /// <summary>
    /// 注册的身份：类型加可选的实例名字
    /// </summary>
    /// <param name="Middleware">中间件类型</param>
    /// <param name="Name">实例名字；按类型登记为空</param>
    private readonly record struct MiddlewareKey(Type Middleware, string? Name);

    /// <summary>
    /// 一条中间件注册：类型、名字、阶段、位置、归属、注册序号、实例解析方法与相对约束
    /// </summary>
    /// <param name="middleware">中间件类型</param>
    /// <param name="name">实例名字；按类型登记为空</param>
    /// <param name="stage">所属阶段</param>
    /// <param name="placement">阶段内位置</param>
    /// <param name="owner">注册者</param>
    /// <param name="sequence">注册序号，同位置同归属时的决胜键</param>
    /// <param name="resolve">每次调用时取得中间件实例的方法</param>
    /// <param name="register">把中间件类型登记进容器的方法；按实例登记为空</param>
    private sealed class Registration(Type middleware, string? name, PipelineStage stage, Placement placement, string owner, int sequence, Func<IServiceProvider, IPipelineMiddleware> resolve, Action<IServiceCollection>? register)
    {
        /// <summary>
        /// 中间件类型
        /// </summary>
        public Type Middleware { get; } = middleware;

        /// <summary>
        /// 实例名字
        /// </summary>
        /// <remarks>
        /// 按类型登记为空
        /// </remarks>
        public string? Name { get; } = name;

        /// <summary>
        /// 所属阶段
        /// </summary>
        public PipelineStage Stage { get; } = stage;

        /// <summary>
        /// 阶段内位置
        /// </summary>
        public Placement Placement { get; } = placement;

        /// <summary>
        /// 注册者（清单 ID 或 <c>app</c>）
        /// </summary>
        public string Owner { get; } = owner;

        /// <summary>
        /// 注册序号
        /// </summary>
        public int Sequence { get; } = sequence;

        /// <summary>
        /// 每次调用时取得中间件实例的方法
        /// </summary>
        public Func<IServiceProvider, IPipelineMiddleware> Resolve { get; } = resolve;

        /// <summary>
        /// 把中间件类型登记进容器的方法
        /// </summary>
        /// <remarks>
        /// 按实例登记为空
        /// </remarks>
        public Action<IServiceCollection>? Register { get; } = register;

        /// <summary>
        /// 必须位于这些中间件之前
        /// </summary>
        public List<Type> Before { get; } = [];

        /// <summary>
        /// 必须位于这些中间件之后
        /// </summary>
        public List<Type> After { get; } = [];

        /// <summary>
        /// 必须位于这些名字的中间件之前
        /// </summary>
        public List<string> BeforeNames { get; } = [];

        /// <summary>
        /// 必须位于这些名字的中间件之后
        /// </summary>
        public List<string> AfterNames { get; } = [];

        /// <summary>
        /// 错误信息里的称呼：类型全名，带名字时附 <c>[名字]</c>
        /// </summary>
        public string Label => Name is null ? Middleware.FullName ?? Middleware.Name : $"{Middleware.FullName}[{Name}]";

        /// <summary>
        /// 对外公开的描述
        /// </summary>
        public MiddlewareDescriptor Descriptor => new(Middleware, Stage, Placement, Owner) { Name = Name, Before = Before, After = After };
    }
}
