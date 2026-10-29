// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 按固定阶段组装执行管道（RFC-0004）。
/// </summary>
/// <remarks>
/// 特性在阶段内登记中间件并声明相对约束，构建时得到确定、可打印的顺序。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PipelineBuilder
{
    /// <summary>
    /// 应用级注册的归属名。
    /// </summary>
    public const string AppOwner = "app";

    /// <summary>
    /// 全部注册，按注册顺序。
    /// </summary>
    private readonly List<Registration> _registrations = [];

    /// <summary>
    /// 按（类型，名字）索引的注册，用于去重。
    /// </summary>
    /// <remarks>
    /// 类型的约束目标经 <see cref="FindByType"/> 解析。
    /// </remarks>
    private readonly Dictionary<MiddlewareKey, Registration> _byKey = [];

    /// <summary>
    /// 在 <see cref="PipelineStage.Transaction"/> 阶段末尾添加中间件。
    /// </summary>
    /// <remarks>
    /// 等价于 <c>UseAt&lt;T&gt;(PipelineStage.Transaction)</c>。
    /// </remarks>
    public PipelineBuilder Use<TMiddleware>()
        where TMiddleware : class, IPipelineMiddleware
        => UseAt<TMiddleware>(PipelineStage.Transaction);

    /// <summary>
    /// 在指定阶段添加从调用作用域解析的中间件。
    /// </summary>
    public PipelineBuilder UseAt<TMiddleware>(PipelineStage stage, Placement placement = Placement.End, string owner = AppOwner)
        where TMiddleware : class, IPipelineMiddleware
        => Add(typeof(TMiddleware), null, stage, placement, owner, static services => services.GetRequiredService<TMiddleware>());

    /// <summary>
    /// 在指定阶段添加一个已创建的中间件实例。
    /// </summary>
    public PipelineBuilder UseInstance<TMiddleware>(TMiddleware instance, PipelineStage stage, Placement placement = Placement.End, string owner = AppOwner)
        where TMiddleware : class, IPipelineMiddleware
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Add(typeof(TMiddleware), null, stage, placement, owner, _ => instance);
    }

    /// <summary>
    /// 在指定阶段添加一个带名字的中间件实例。
    /// </summary>
    /// <remarks>
    /// 同一类型可按不同名字登记多次（如插件贡献的多个中间件），名字在同类型内唯一。按类型声明的相对约束（<see cref="Before{TMiddleware, TOther}"/> / <see cref="After{TMiddleware, TOther}"/>）在该类型有多个实例时不可用。
    /// </remarks>
    public PipelineBuilder UseNamedInstance(string name, IPipelineMiddleware instance, PipelineStage stage, Placement placement = Placement.End, string owner = AppOwner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(instance);
        return Add(instance.GetType(), name, stage, placement, owner, _ => instance);
    }

    /// <summary>
    /// 约束 <typeparamref name="TMiddleware"/> 位于 <typeparamref name="TOther"/> 之前。
    /// </summary>
    /// <remarks>
    /// 两者必须在同一阶段。跨包依赖必须这样声明：阶段内的基准次序（归属清单 ID 字典序）只保证可复现，不表达依赖。
    /// </remarks>
    public PipelineBuilder Before<TMiddleware, TOther>()
        where TMiddleware : class, IPipelineMiddleware
        where TOther : class, IPipelineMiddleware
    {
        var registration = Require(typeof(TMiddleware));
        registration.Before.Add(typeof(TOther));
        return this;
    }

    /// <summary>
    /// 约束 <typeparamref name="TMiddleware"/> 位于 <typeparamref name="TOther"/> 之后。
    /// </summary>
    /// <remarks>
    /// 两者必须在同一阶段。要用到另一个包中间件产出（认证后的主体、解析后的租户）的中间件必须这样声明，基准次序不表达依赖。
    /// </remarks>
    public PipelineBuilder After<TMiddleware, TOther>()
        where TMiddleware : class, IPipelineMiddleware
        where TOther : class, IPipelineMiddleware
    {
        var registration = Require(typeof(TMiddleware));
        registration.After.Add(typeof(TOther));
        return this;
    }

    /// <summary>
    /// 计算最终顺序。
    /// </summary>
    /// <remarks>
    /// 约束成环抛出 <see cref="KernelErrors.PipelineCycle"/>，约束引用未注册或跨阶段的中间件抛出 <see cref="KernelErrors.PipelineRegistration"/>。
    /// </remarks>
    public IReadOnlyList<MiddlewareDescriptor> Describe() => Order().Select(static r => r.Descriptor).ToList();

    /// <summary>
    /// 按 RFC-0004 的格式打印最终顺序，一行一个中间件。
    /// </summary>
    /// <remarks>
    /// 带名字的实例在类型名后以 <c>[名字]</c> 标出。
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
    /// 构建管道委托。
    /// </summary>
    /// <remarks>
    /// 每次调用时中间件从 <see cref="InvocationContext.Services"/> 解析；<paramref name="terminal"/> 在所有中间件之后执行。
    /// </remarks>
    public PipelineStep Build(PipelineStep terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        var ordered = Order();
        // 从终点向前逐层包裹：每一层捕获它的下一步，最终返回最外层（第一个中间件）的委托。
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
    /// 登记一个中间件。
    /// </summary>
    /// <remarks>
    /// 校验阶段合法、（类型，名字）未重复，并记录注册序号作为最终排序的决胜键。
    /// </remarks>
    /// <param name="middleware">中间件类型。</param>
    /// <param name="name">实例名字；按类型登记为空。</param>
    /// <param name="stage">所属阶段。</param>
    /// <param name="placement">阶段内位置。</param>
    /// <param name="owner">注册者。</param>
    /// <param name="resolve">每次调用时取得中间件实例的方法。</param>
    private PipelineBuilder Add(Type middleware, string? name, PipelineStage stage, Placement placement, string owner, Func<IServiceProvider, IPipelineMiddleware> resolve)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (!Enum.IsDefined(stage))
        {
            throw new FrameworkException(KernelErrors.UnknownStage, $"Unknown pipeline stage '{stage}'.");
        }

        var key = new MiddlewareKey(middleware, name);
        if (_byKey.ContainsKey(key))
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, name is null
                ? $"Middleware '{middleware.FullName}' is already registered; each middleware type may appear only once in a pipeline unless its instances are named."
                : $"Middleware '{middleware.FullName}' named '{name}' is already registered; instance names must be unique within a type.");
        }

        var registration = new Registration(middleware, name, stage, placement, owner, _registrations.Count, resolve);
        _registrations.Add(registration);
        _byKey[key] = registration;
        return this;
    }

    /// <summary>
    /// 取按类型唯一确定的注册。
    /// </summary>
    /// <remarks>
    /// 未注册时抛出 <see cref="KernelErrors.PipelineRegistration"/>。约束必须在注册之后声明。
    /// </remarks>
    private Registration Require(Type middleware)
        => FindByType(middleware) ?? throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{middleware.FullName}' is not registered, so no ordering constraint can be declared for it.");

    /// <summary>
    /// 按类型找注册。
    /// </summary>
    /// <remarks>
    /// 恰好一个返回它；没有返回空；多个（按名字登记的多个实例）抛出，因为按类型的约束无法指向其中某一个。
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
    /// 计算全部中间件的最终顺序。
    /// </summary>
    /// <remarks>
    /// 按阶段枚举顺序逐阶段处理，阶段内先按位置、归属、注册序号排出基准次序，再按相对约束调整。
    /// </remarks>
    private List<Registration> Order()
    {
        var result = new List<Registration>(_registrations.Count);
        foreach (var stage in PipelineStageExtensions.All)
        {
            // 归属按序号比较而不是注册顺序：同阶段里不同包的中间件顺序与 UseFeature 的调用顺序无关，结果可复现。
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
    /// 在一个阶段内按 Before/After 约束做稳定拓扑排序。
    /// </summary>
    /// <remarks>
    /// 无约束时保持 <paramref name="candidates"/> 的次序，约束成环抛出 <see cref="KernelErrors.PipelineCycle"/>。
    /// </remarks>
    /// <param name="candidates">该阶段的中间件，已按基准次序排好。</param>
    /// <param name="stage">所属阶段，用于错误信息与跨阶段校验。</param>
    private List<Registration> SortByConstraints(List<Registration> candidates, PipelineStage stage)
    {
        // 边：from 必须先于 to。入度为零的按原有次序出队，保证确定性；节点以注册对象为键，同类型的多个命名实例互不混淆。
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
                    // 新就绪的节点按基准次序插回队列，而不是追加到末尾，这样约束只改变必要的相对位置。
                    var insertAt = ready.FindIndex(r => candidates.IndexOf(r) > candidates.IndexOf(successor));
                    ready.Insert(insertAt < 0 ? ready.Count : insertAt, successor);
                }
            }
        }

        // 有节点始终入度不为零，说明约束之间成环；把仍被卡住的节点列出来。
        if (ordered.Count != candidates.Count)
        {
            var stuck = candidates.Where(r => indegree[r] > 0).Select(static r => $"'{r.Middleware.FullName}'");
            throw new FrameworkException(KernelErrors.PipelineCycle, $"Middleware ordering constraints in stage '{stage.ToManifestName()}' form a cycle: {string.Join(", ", stuck)}.");
        }

        return ordered;
    }

    /// <summary>
    /// 解析约束指向的中间件。
    /// </summary>
    /// <remarks>
    /// 必须已注册、按类型唯一且与 <paramref name="source"/> 在同一阶段，否则抛出 <see cref="KernelErrors.PipelineRegistration"/>。
    /// </remarks>
    /// <param name="source">声明约束的中间件。</param>
    /// <param name="target">约束指向的中间件类型。</param>
    /// <param name="stage">当前阶段。</param>
    private Registration ResolveConstraintTarget(Registration source, Type target, PipelineStage stage)
    {
        var other = FindByType(target) ?? throw new FrameworkException(KernelErrors.PipelineRegistration, $"The ordering constraint of middleware '{source.Middleware.FullName}' references unregistered middleware '{target.FullName}'.");

        // 跨阶段的先后由阶段顺序决定，相对约束只在阶段内有意义。
        if (other.Stage != stage)
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, $"Middleware '{source.Middleware.FullName}' (stage '{stage.ToManifestName()}') and '{target.FullName}' (stage '{other.Stage.ToManifestName()}') are not in the same stage, so no relative order can be declared between them.");
        }

        return other;
    }

    /// <summary>
    /// 注册的身份：类型加可选的实例名字。
    /// </summary>
    /// <param name="Middleware">中间件类型。</param>
    /// <param name="Name">实例名字；按类型登记为空。</param>
    private readonly record struct MiddlewareKey(Type Middleware, string? Name);

    /// <summary>
    /// 一条中间件注册：类型、名字、阶段、位置、归属、注册序号、实例解析方法与相对约束。
    /// </summary>
    /// <param name="middleware">中间件类型。</param>
    /// <param name="name">实例名字；按类型登记为空。</param>
    /// <param name="stage">所属阶段。</param>
    /// <param name="placement">阶段内位置。</param>
    /// <param name="owner">注册者。</param>
    /// <param name="sequence">注册序号，同位置同归属时的决胜键。</param>
    /// <param name="resolve">每次调用时取得中间件实例的方法。</param>
    private sealed class Registration(Type middleware, string? name, PipelineStage stage, Placement placement, string owner, int sequence, Func<IServiceProvider, IPipelineMiddleware> resolve)
    {
        /// <summary>
        /// 中间件类型。
        /// </summary>
        public Type Middleware { get; } = middleware;

        /// <summary>
        /// 实例名字。
        /// </summary>
        /// <remarks>
        /// 按类型登记为空。
        /// </remarks>
        public string? Name { get; } = name;

        /// <summary>
        /// 所属阶段。
        /// </summary>
        public PipelineStage Stage { get; } = stage;

        /// <summary>
        /// 阶段内位置。
        /// </summary>
        public Placement Placement { get; } = placement;

        /// <summary>
        /// 注册者（清单 ID 或 <c>app</c>）。
        /// </summary>
        public string Owner { get; } = owner;

        /// <summary>
        /// 注册序号。
        /// </summary>
        public int Sequence { get; } = sequence;

        /// <summary>
        /// 每次调用时取得中间件实例的方法。
        /// </summary>
        public Func<IServiceProvider, IPipelineMiddleware> Resolve { get; } = resolve;

        /// <summary>
        /// 必须位于这些中间件之前。
        /// </summary>
        public List<Type> Before { get; } = [];

        /// <summary>
        /// 必须位于这些中间件之后。
        /// </summary>
        public List<Type> After { get; } = [];

        /// <summary>
        /// 对外公开的描述。
        /// </summary>
        public MiddlewareDescriptor Descriptor => new(Middleware, Stage, Placement, Owner) { Name = Name, Before = Before, After = After };
    }
}
