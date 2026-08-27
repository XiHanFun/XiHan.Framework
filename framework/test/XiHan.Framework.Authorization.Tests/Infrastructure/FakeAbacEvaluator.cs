// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Abac;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// ABAC 评估器替身
/// </summary>
/// <remarks>
/// 结果由构造参数固定，同时保留最近一次评估上下文，便于断言处理器组装上下文的字段来源。
/// </remarks>
public sealed class FakeAbacEvaluator : IAbacEvaluator
{
    private readonly bool _allowed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="allowed">固定返回的评估结论</param>
    public FakeAbacEvaluator(bool allowed)
    {
        _allowed = allowed;
    }

    /// <summary>
    /// 调用次数
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 最近一次收到的评估上下文
    /// </summary>
    public AbacEvaluationContext? LastContext { get; private set; }

    /// <summary>
    /// 评估 ABAC
    /// </summary>
    /// <param name="context">评估上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    public Task<AbacEvaluationResult> EvaluateAsync(AbacEvaluationContext context, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastContext = context;
        return Task.FromResult(_allowed
            ? AbacEvaluationResult.Allow("替身放行")
            : AbacEvaluationResult.Deny("替身拒绝"));
    }
}
