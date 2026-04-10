#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAbacEvaluator
// Guid:b10dcd34-8f26-4db3-91ad-e27f17cf0394
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authorization.Abac;

/// <summary>
/// ABAC 评估器接口
/// </summary>
public interface IAbacEvaluator
{
    /// <summary>
    /// 评估 ABAC
    /// </summary>
    /// <param name="context">评估上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    Task<AbacEvaluationResult> EvaluateAsync(AbacEvaluationContext context, CancellationToken cancellationToken = default);
}
