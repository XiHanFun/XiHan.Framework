// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道中一个中间件的描述：类型、阶段、位置、归属与相对约束。
/// </summary>
/// <param name="Middleware">中间件类型。</param>
/// <param name="Stage">所属阶段。</param>
/// <param name="Placement">阶段内位置。</param>
/// <param name="Owner">注册者（清单 ID 或 <c>app</c>）。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record MiddlewareDescriptor(Type Middleware, PipelineStage Stage, Placement Placement, string Owner)
{
    /// <summary>
    /// 必须位于这些中间件之前。
    /// </summary>
    public IReadOnlyList<Type> Before { get; init; } = [];

    /// <summary>
    /// 必须位于这些中间件之后。
    /// </summary>
    public IReadOnlyList<Type> After { get; init; } = [];
}

/// <summary>
/// 管道构建器。中间件按固定阶段插入，阶段内按 <see cref="Placement"/>、相对约束、归属与注册顺序确定排序；结果可打印。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class PipelineBuilder
{
    /// <summary>
    /// 应用级注册的归属名。
    /// </summary>
    public const string AppOwner = "app";

    private readonly List<Registration> _registrations = [];
    private readonly Dictionary<Type, Registration> _byType = [];

    /// <summary>
    /// 在 <see cref="PipelineStage.Transaction"/> 阶段末尾添加中间件；等价于 <c>UseAt&lt;T&gt;(PipelineStage.Transaction)</c>。
    /// </summary>
    public PipelineBuilder Use<TMiddleware>()
        where TMiddleware : class, IPipelineMiddleware
        => UseAt<TMiddleware>(PipelineStage.Transaction);

    /// <summary>
    /// 在指定阶段添加从调用作用域解析的中间件。
    /// </summary>
    public PipelineBuilder UseAt<TMiddleware>(PipelineStage stage, Placement placement = Placement.End, string owner = AppOwner)
        where TMiddleware : class, IPipelineMiddleware
        => Add(typeof(TMiddleware), stage, placement, owner, static services => services.GetRequiredService<TMiddleware>());

    /// <summary>
    /// 在指定阶段添加一个已创建的中间件实例。
    /// </summary>
    public PipelineBuilder UseInstance<TMiddleware>(TMiddleware instance, PipelineStage stage, Placement placement = Placement.End, string owner = AppOwner)
        where TMiddleware : class, IPipelineMiddleware
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Add(typeof(TMiddleware), stage, placement, owner, _ => instance);
    }

    /// <summary>
    /// 约束 <typeparamref name="TMiddleware"/> 位于 <typeparamref name="TOther"/> 之前；两者必须在同一阶段。
    /// </summary>
    public PipelineBuilder Before<TMiddleware, TOther>()
        where TMiddleware : class, IPipelineMiddleware
        where TOther : class, IPipelineMiddleware
    {
        var registration = Require(typeof(TMiddleware));
        registration.Before.Add(typeof(TOther));
        return this;
    }

    /// <summary>
    /// 约束 <typeparamref name="TMiddleware"/> 位于 <typeparamref name="TOther"/> 之后；两者必须在同一阶段。
    /// </summary>
    public PipelineBuilder After<TMiddleware, TOther>()
        where TMiddleware : class, IPipelineMiddleware
        where TOther : class, IPipelineMiddleware
    {
        var registration = Require(typeof(TMiddleware));
        registration.After.Add(typeof(TOther));
        return this;
    }

    /// <summary>
    /// 计算最终顺序。约束成环抛出 <see cref="KernelErrors.PipelineCycle"/>，约束引用未注册或跨阶段的中间件抛出 <see cref="KernelErrors.PipelineRegistration"/>。
    /// </summary>
    public IReadOnlyList<MiddlewareDescriptor> Describe() => Order().Select(static r => r.Descriptor).ToList();

    /// <summary>
    /// 按 RFC-0004 的格式打印最终顺序，一行一个中间件。
    /// </summary>
    public string Dump()
    {
        var builder = new StringBuilder();
        foreach (var descriptor in Describe())
        {
            builder.Append(descriptor.Stage.ToManifestName().PadRight(13)).Append(descriptor.Middleware.FullName).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// 构建管道委托。每次调用时中间件从 <see cref="InvocationContext.Services"/> 解析；<paramref name="terminal"/> 在所有中间件之后执行。
    /// </summary>
    public PipelineStep Build(PipelineStep terminal)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        var ordered = Order();
        var next = terminal;
        for (var i = ordered.Count - 1; i >= 0; i--)
        {
            var resolve = ordered[i].Resolve;
            var captured = next;
            next = context => resolve(context.Services).InvokeAsync(context, captured);
        }

        return next;
    }

    private PipelineBuilder Add(Type middleware, PipelineStage stage, Placement placement, string owner, Func<IServiceProvider, IPipelineMiddleware> resolve)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (!Enum.IsDefined(stage))
        {
            throw new FrameworkException(KernelErrors.UnknownStage, $"未知的管道阶段 {stage}。");
        }

        if (_byType.ContainsKey(middleware))
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, $"中间件 {middleware.FullName} 已注册，同一管道中每个中间件类型只能出现一次。");
        }

        var registration = new Registration(middleware, stage, placement, owner, _registrations.Count, resolve);
        _registrations.Add(registration);
        _byType[middleware] = registration;
        return this;
    }

    private Registration Require(Type middleware)
        => _byType.TryGetValue(middleware, out var registration)
            ? registration
            : throw new FrameworkException(KernelErrors.PipelineRegistration, $"中间件 {middleware.FullName} 尚未注册，不能对它声明顺序约束。");

    private List<Registration> Order()
    {
        var result = new List<Registration>(_registrations.Count);
        foreach (var stage in PipelineStageExtensions.All)
        {
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

    private List<Registration> SortByConstraints(List<Registration> candidates, PipelineStage stage)
    {
        // 边：from 必须先于 to。入度为零的按原有次序出队，保证确定性。
        var successors = candidates.ToDictionary(static r => r.Middleware, static _ => new List<Registration>());
        var indegree = candidates.ToDictionary(static r => r.Middleware, static _ => 0);

        foreach (var registration in candidates)
        {
            foreach (var later in registration.Before)
            {
                var other = ResolveConstraintTarget(registration, later, stage);
                successors[registration.Middleware].Add(other);
                indegree[other.Middleware]++;
            }

            foreach (var earlier in registration.After)
            {
                var other = ResolveConstraintTarget(registration, earlier, stage);
                successors[other.Middleware].Add(registration);
                indegree[registration.Middleware]++;
            }
        }

        var ordered = new List<Registration>(candidates.Count);
        var ready = new List<Registration>(candidates.Where(r => indegree[r.Middleware] == 0));
        while (ready.Count > 0)
        {
            var current = ready[0];
            ready.RemoveAt(0);
            ordered.Add(current);
            foreach (var successor in successors[current.Middleware])
            {
                if (--indegree[successor.Middleware] == 0)
                {
                    var insertAt = ready.FindIndex(r => candidates.IndexOf(r) > candidates.IndexOf(successor));
                    ready.Insert(insertAt < 0 ? ready.Count : insertAt, successor);
                }
            }
        }

        if (ordered.Count != candidates.Count)
        {
            var stuck = candidates.Where(r => indegree[r.Middleware] > 0).Select(static r => r.Middleware.FullName);
            throw new FrameworkException(KernelErrors.PipelineCycle, $"阶段 {stage.ToManifestName()} 的中间件顺序约束成环：{string.Join("、", stuck)}。");
        }

        return ordered;
    }

    private Registration ResolveConstraintTarget(Registration source, Type target, PipelineStage stage)
    {
        if (!_byType.TryGetValue(target, out var other))
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, $"{source.Middleware.FullName} 的顺序约束引用了未注册的中间件 {target.FullName}。");
        }

        if (other.Stage != stage)
        {
            throw new FrameworkException(KernelErrors.PipelineRegistration, $"{source.Middleware.FullName}（{stage.ToManifestName()}）与 {target.FullName}（{other.Stage.ToManifestName()}）不在同一阶段，不能声明相对顺序。");
        }

        return other;
    }

    private sealed class Registration(Type middleware, PipelineStage stage, Placement placement, string owner, int sequence, Func<IServiceProvider, IPipelineMiddleware> resolve)
    {
        public Type Middleware { get; } = middleware;

        public PipelineStage Stage { get; } = stage;

        public Placement Placement { get; } = placement;

        public string Owner { get; } = owner;

        public int Sequence { get; } = sequence;

        public Func<IServiceProvider, IPipelineMiddleware> Resolve { get; } = resolve;

        public List<Type> Before { get; } = [];

        public List<Type> After { get; } = [];

        public MiddlewareDescriptor Descriptor => new(Middleware, Stage, Placement, Owner) { Before = Before, After = After };
    }
}
