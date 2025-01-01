#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHasSimpleStateCheckers
// Guid:b47451b9-b6bd-475d-86bf-2b0a9a431ab7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 23:13:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
