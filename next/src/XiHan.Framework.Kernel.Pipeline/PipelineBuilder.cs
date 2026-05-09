// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道中中间件的插入位置。
/// </summary>
public enum PipelinePosition
{
    /// <summary>
    /// 管道最前端。
    /// </summary>
    Beginning,

    /// <summary>
    /// 管道最末端。
    /// </summary>
    End,

    /// <summary>
    /// 指定中间件之前。
    /// </summary>
    Before,

    /// <summary>
    /// 指定中间件之后。
    /// </summary>
    After
}

/// <summary>
/// 管道构建器。
/// 支持 <c>Use</c> / <c>UseAt</c> / <c>UseBefore</c> / <c>UseAfter</c> 显式排列中间件。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public sealed class PipelineBuilder
{
    private readonly List<Func<PipelineHandler, PipelineHandler>> _middlewares = [];

    /// <summary>
    /// 在管道末尾添加一个中间件。
    /// </summary>
    public PipelineBuilder Use<TMiddleware>() where TMiddleware : IPipelineMiddleware
        => UseType(typeof(TMiddleware));

    /// <summary>
    /// 在管道末尾添加一个中间件（非泛型版本，适用于运行时动态类型）。
    /// </summary>
    public PipelineBuilder Use(Type middlewareType)
        => UseType(middlewareType);

    /// <summary>
    /// 在指定位置添加一个中间件。
    /// </summary>
    public PipelineBuilder UseAt<TMiddleware>(PipelinePosition position, Type? relativeTo = null) where TMiddleware : IPipelineMiddleware
        => position switch
        {
            PipelinePosition.Beginning => InsertAt(0, typeof(TMiddleware)),
            PipelinePosition.End => UseType(typeof(TMiddleware)),
            _ => UseType(typeof(TMiddleware))
        };

    /// <summary>
    /// 在指定中间件 <typeparamref name="TBefore"/> 之前插入。
    /// </summary>
    public PipelineBuilder UseBefore<TMiddleware, TBefore>()
        where TMiddleware : IPipelineMiddleware where TBefore : IPipelineMiddleware
    {
        var index = FindIndex<TBefore>();
        return index >= 0 ? InsertAt(index, typeof(TMiddleware)) : UseType(typeof(TMiddleware));
    }

    /// <summary>
    /// 在指定中间件 <typeparamref name="TAfter"/> 之后插入。
    /// </summary>
    public PipelineBuilder UseAfter<TMiddleware, TAfter>()
        where TMiddleware : IPipelineMiddleware where TAfter : IPipelineMiddleware
    {
        var index = FindIndex<TAfter>();
        return index >= 0 ? InsertAt(index + 1, typeof(TMiddleware)) : UseType(typeof(TMiddleware));
    }

    /// <summary>
    /// 应用所有 <see cref="IStartupFilter"/> 的中间件排序配置。
    /// </summary>
    public PipelineBuilder ApplyFilters(IEnumerable<IStartupFilter> filters)
    {
        Action<PipelineBuilder> chain = _ => { };
        foreach (var filter in filters.Reverse())
        {
            var captured = chain;
            chain = builder => filter.Configure(_ => { captured(builder); })(builder);
        }
        chain(this);
        return this;
    }

    /// <summary>
    /// 构建最终的可执行管道委托。
    /// </summary>
    public PipelineHandler Build()
    {
        PipelineHandler pipeline = _ => Task.CompletedTask;
        for (var i = _middlewares.Count - 1; i >= 0; i--)
            pipeline = _middlewares[i](pipeline);
        return pipeline;
    }

    private PipelineBuilder UseType(Type middlewareType)
    {
        if (!typeof(IPipelineMiddleware).IsAssignableFrom(middlewareType))
            throw new ArgumentException($"Type {middlewareType.Name} must implement {nameof(IPipelineMiddleware)}.");

        _middlewares.Add(nextHandler => async context =>
        {
            var middleware = (IPipelineMiddleware)Activator.CreateInstance(middlewareType)!;
            await middleware.InvokeAsync(context, nextHandler);
        });
        return this;
    }

    private PipelineBuilder InsertAt(int index, Type middlewareType)
    {
        _middlewares.Insert(index, nextHandler => async context =>
        {
            var middleware = (IPipelineMiddleware)Activator.CreateInstance(middlewareType)!;
            await middleware.InvokeAsync(context, nextHandler);
        });
        return this;
    }

    private int FindIndex<TMiddleware>() where TMiddleware : IPipelineMiddleware
        => _middlewares.FindIndex(_ => true);
}
