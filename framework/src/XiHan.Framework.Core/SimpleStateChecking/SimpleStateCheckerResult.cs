#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SimpleStateCheckerResult
// Guid:5d82066b-e39b-432d-8ecb-e9f6993ac37f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/24 23:16:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单状态检查器结果
/// </summary>
public class SimpleStateCheckerResult<TState> : Dictionary<TState, bool>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SimpleStateCheckerResult()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="states"></param>
    /// <param name="initValue"></param>
    public SimpleStateCheckerResult(IEnumerable<TState> states, bool initValue = true)
    {
        foreach (var state in states)
        {
            Add(state, initValue);
        }
    }
}
