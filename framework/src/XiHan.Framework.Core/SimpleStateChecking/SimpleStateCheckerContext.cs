#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SimpleStateCheckerContext
// Guid:b3c15ad8-f16b-45df-9719-c83fd80018b3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/24 23:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单状态检查器上下文
/// </summary>
public class SimpleStateCheckerContext<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <param name="state"></param>
    public SimpleStateCheckerContext(IServiceProvider serviceProvider, TState state)
    {
        ServiceProvider = serviceProvider;
        State = state;
    }

    /// <summary>
    /// 服务提供者
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 状态
    /// </summary>
    public TState State { get; }
}
