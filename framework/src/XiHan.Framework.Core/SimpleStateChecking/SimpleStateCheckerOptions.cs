#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SimpleStateCheckerOptions
// Guid:e92b4b45-e14f-4191-8bbd-5a23b0da0fa7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/24 23:18:53
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
