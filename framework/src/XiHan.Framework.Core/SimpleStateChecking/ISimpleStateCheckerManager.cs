// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单状态检查器管理器接口
/// </summary>
public interface ISimpleStateCheckerManager<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    Task<bool> IsEnabledAsync(TState state);

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="states"></param>
    /// <returns></returns>
    Task<SimpleStateCheckerResult<TState>> IsEnabledAsync(TState[] states);
}
