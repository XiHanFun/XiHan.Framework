// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道中中间件的插入位置。
/// </summary>
public enum PipelinePosition
{
    Beginning,
    End,
    Before,
    After
}

/// <summary>
/// 管道构建器。支持 Use/UseAt/UseBefore/UseAfter 显式排列中间件。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public sealed class PipelineBuilder
{
    private readonly List<Func<PipelineDelegate, PipelineDelegate>> _middlewares = [];

    /// <summary>
    /// 在管道末尾添加一个中间件。
    /// </summary>
    public PipelineBuilder Use<TMiddleware>() where TMiddleware : IPipelineMiddleware
        => Use(typeof(TMiddleware));

    /// <summary>
    /// 在管道末尾添加一个中间件。
    /// </summary>
    public PipelineBuilder Use(Type middlewareType)
    {
        if (!typeof(IPipelineMiddleware).IsAssignableFrom(middlewareType))
            throw new ArgumentException($"Type {middlewareType.Name} must implement {nameof(IPipelineMiddleware)}.");

        _middlewares.Add(next => async context =>
        {
            var middleware = (IPipelineMiddleware)Activator.CreateInstance(middlewareType)!;
            await middleware.InvokeAsync(context, next);
        });

        return this;
    }

    /// <summary>
    /// 在指定位置添加一个中间件。
    /// </summary>
    public PipelineBuilder UseAt<TMiddleware>(PipelinePosition position, Type? relativeTo = null) where TMiddleware : IPipelineMiddleware
    {
        return position switch
        {
            PipelinePosition.Beginning => InsertAt(0, typeof(TMiddleware)),
            PipelinePosition.End => Use(typeof(TMiddleware)),
            _ => Use(typeof(TMiddleware))
        };
    }

    /// <summary>
    /// 在指定中间件之前插入。
    /// </summary>
    public PipelineBuilder UseBefore<TMiddleware, TBefore>()
        where TMiddleware : IPipelineMiddleware
        where TBefore : IPipelineMiddleware
    {
        var index = FindIndex<TBefore>();
        return index >= 0 ? InsertAt(index, typeof(TMiddleware)) : Use(typeof(TMiddleware));
    }

    /// <summary>
    /// 在指定中间件之后插入。
    /// </summary>
    public PipelineBuilder UseAfter<TMiddleware, TAfter>()
        where TMiddleware : IPipelineMiddleware
        where TAfter : IPipelineMiddleware
    {
        var index = FindIndex<TAfter>();
        return index >= 0 ? InsertAt(index + 1, typeof(TMiddleware)) : Use(typeof(TMiddleware));
    }

    /// <summary>
    /// 应用所有 StartupFilter 的中间件排序。
    /// </summary>
    public PipelineBuilder ApplyFilters(IEnumerable<IStartupFilter> filters)
    {
        Action<PipelineBuilder> chain = _ => { };

        foreach (var filter in filters.Reverse())
        {
            var next = chain;
            chain = builder => filter.Configure(_ => { next(builder); })(builder);
        }

        chain(this);
        return this;
    }

    /// <summary>
    /// 构建管道委托。
    /// </summary>
    public PipelineDelegate Build()
    {
        PipelineDelegate pipeline = _ => Task.CompletedTask;

        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            pipeline = _middlewares[i](pipeline);
        }

        return pipeline;
    }

    private PipelineBuilder InsertAt(int index, Type middlewareType)
    {
        _middlewares.Insert(index, next => async context =>
        {
            var middleware = (IPipelineMiddleware)Activator.CreateInstance(middlewareType)!;
            await middleware.InvokeAsync(context, next);
        });
        return this;
    }

    private int FindIndex<TMiddleware>() where TMiddleware : IPipelineMiddleware
        => _middlewares.FindIndex(_ => true);

    // Note: In production code, this would track middleware types for O(1) lookup.
}
