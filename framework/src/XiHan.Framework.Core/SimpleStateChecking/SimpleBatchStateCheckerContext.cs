#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SimpleBatchStateCheckerContext
// Guid:1ca1dede-d837-46c3-acee-2067291ad772
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 23:23:46
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单批量状态检查器上下文
/// </summary>
public class SimpleBatchStateCheckerContext<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 服务提供程序
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 状态
    /// </summary>
    public TState[] States { get; }

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
}
