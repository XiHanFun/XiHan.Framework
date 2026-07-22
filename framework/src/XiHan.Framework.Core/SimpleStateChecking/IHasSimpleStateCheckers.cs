// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 具有简单状态检查器接口接口
/// </summary>
public interface IHasSimpleStateCheckers<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 状态检查器列表
    /// </summary>
    List<ISimpleStateChecker<TState>> StateCheckers { get; }
}
