// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道中中间件的插入位置。
/// </summary>
public enum PipelinePosition
{
    /// <summary>
    /// 最前端。
    /// </summary>
    Beginning,

    /// <summary>
    /// 最末端。
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
/// 管道构建器。支持 Use / UseAt / UseBefore / UseAfter 显式排列中间件。
/// </summary>
[ApiLevel(Stability.Stable, "1.0")]
public sealed class PipelineBuilder
{
    private readonly List<Func<PipelineHandler, PipelineHandler>> _middlewares = [];
    private readonly List<Type> _middlewareTypes = [];

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
    {
        return position switch
        {
            PipelinePosition.Beginning => InsertAt(0, typeof(TMiddleware)),
            PipelinePosition.End => UseType(typeof(TMiddleware)),
            PipelinePosition.Before => relativeTo is not null ? InsertAt(FindIndexOf(relativeTo), typeof(TMiddleware)) : UseType(typeof(TMiddleware)),
            PipelinePosition.After => relativeTo is not null ? InsertAt(FindIndexOf(relativeTo) + 1, typeof(TMiddleware)) : UseType(typeof(TMiddleware)),
            _ => UseType(typeof(TMiddleware))
        };
    }

    /// <summary>
    /// 在指定中间件 <typeparamref name="TBefore"/> 之前插入。
    /// </summary>
    public PipelineBuilder UseBefore<TMiddleware, TBefore>()
        where TMiddleware : IPipelineMiddleware where TBefore : IPipelineMiddleware
    {
        var index = FindIndexOf<TBefore>();
        if (index < 0)
            throw new InvalidOperationException($"Middleware '{typeof(TBefore).Name}' not found in pipeline. Add it before using UseBefore.");
        return InsertAt(index, typeof(TMiddleware));
    }

    /// <summary>
    /// 在指定中间件 <typeparamref name="TAfter"/> 之后插入。
    /// </summary>
    public PipelineBuilder UseAfter<TMiddleware, TAfter>()
        where TMiddleware : IPipelineMiddleware where TAfter : IPipelineMiddleware
    {
        var index = FindIndexOf<TAfter>();
        if (index < 0)
            throw new InvalidOperationException($"Middleware '{typeof(TAfter).Name}' not found in pipeline. Add it before using UseAfter.");
        return InsertAt(index + 1, typeof(TMiddleware));
    }

    /// <summary>
    /// 应用所有 IStartupFilter 的中间件排序配置。
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

    private static void ValidateMiddlewareType(Type type)
    {
        if (!typeof(IPipelineMiddleware).IsAssignableFrom(type))
            throw new ArgumentException($"Type {type.Name} must implement {nameof(IPipelineMiddleware)}.");
    }

    private static Func<PipelineHandler, PipelineHandler> CreateFactory(Type type)
        => nextHandler => async context =>
        {
            var middleware = (IPipelineMiddleware)Activator.CreateInstance(type)!;
            await middleware.InvokeAsync(context, nextHandler);
        };

    private PipelineBuilder UseType(Type middlewareType)
    {
        ValidateMiddlewareType(middlewareType);
        _middlewareTypes.Add(middlewareType);
        _middlewares.Add(CreateFactory(middlewareType));
        return this;
    }

    private PipelineBuilder InsertAt(int index, Type middlewareType)
    {
        ValidateMiddlewareType(middlewareType);
        _middlewareTypes.Insert(index, middlewareType);
        _middlewares.Insert(index, CreateFactory(middlewareType));
        return this;
    }

    private int FindIndexOf(Type middlewareType)
        => _middlewareTypes.FindIndex(t => t == middlewareType);

    private int FindIndexOf<TMiddleware>() where TMiddleware : IPipelineMiddleware
        => FindIndexOf(typeof(TMiddleware));
}
