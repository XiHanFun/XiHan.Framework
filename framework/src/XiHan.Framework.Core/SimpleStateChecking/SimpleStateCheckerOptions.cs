// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Collections;

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单状态检查器选项
/// </summary>
public class SimpleStateCheckerOptions<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SimpleStateCheckerOptions()
    {
        GlobalStateCheckers = new TypeList<ISimpleStateChecker<TState>>();
    }

    /// <summary>
    /// 全局状态检查器
    /// </summary>
    public ITypeList<ISimpleStateChecker<TState>> GlobalStateCheckers { get; }
}
