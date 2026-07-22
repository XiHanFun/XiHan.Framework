// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
