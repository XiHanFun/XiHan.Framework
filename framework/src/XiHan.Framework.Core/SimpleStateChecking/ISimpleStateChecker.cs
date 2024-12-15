#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISimpleStateChecker
// Guid:a589a3d7-3fed-47bd-8f82-288bb33be41d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 23:14:26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单状态检查器接口
/// </summary>
public interface ISimpleStateChecker<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task<bool> IsEnabledAsync(SimpleStateCheckerContext<TState> context);
}
