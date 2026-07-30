// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单批量状态检查器上下文
/// </summary>
public class SimpleBatchStateCheckerContext<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="states"></param>
    public SimpleBatchStateCheckerContext(IServiceProvider serviceProvider, TState[] states)
    {
        ServiceProvider = serviceProvider;
        States = states;
    }

    /// <summary>
    /// 服务提供程序
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 状态
    /// </summary>
    public TState[] States { get; }
}
