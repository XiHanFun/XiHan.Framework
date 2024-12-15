#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISimpleBatchStateChecker
// Guid:73f61c65-5224-4e10-8f51-f368b34db6aa
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/24 23:23:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.SimpleStateChecking;

/// <summary>
/// 简单批量状态检查器接口
/// </summary>
public interface ISimpleBatchStateChecker<TState> : ISimpleStateChecker<TState>
    where TState : IHasSimpleStateCheckers<TState>
{
    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    Task<SimpleStateCheckerResult<TState>> IsEnabledAsync(SimpleBatchStateCheckerContext<TState> context);
}
