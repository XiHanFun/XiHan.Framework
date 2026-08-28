// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Policies;

namespace XiHan.Framework.Authorization.Tests.Infrastructure;

/// <summary>
/// 委托式自定义授权要求
/// </summary>
/// <remarks>
/// 自定义要求是策略评估里唯一的扩展点，这里把判定逻辑外置成委托，同时把评估时拿到的上下文原样留存，
/// 用于断言评估器给上下文填了哪些字段。
/// </remarks>
public sealed class DelegateAuthorizationRequirement : IAuthorizationRequirement
{
    private readonly Func<AuthorizationContext, bool> _predicate;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name">要求名称</param>
    /// <param name="predicate">判定委托</param>
    public DelegateAuthorizationRequirement(string name, Func<AuthorizationContext, bool> predicate)
    {
        Name = name;
        _predicate = predicate;
    }

    /// <summary>
    /// 要求名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 评估时收到的上下文
    /// </summary>
    public AuthorizationContext? CapturedContext { get; private set; }

    /// <summary>
    /// 评估次数
    /// </summary>
    public int EvaluateCount { get; private set; }

    /// <summary>
    /// 评估授权要求
    /// </summary>
    /// <param name="context">授权上下文</param>
    /// <returns>是否满足要求</returns>
    public Task<bool> EvaluateAsync(AuthorizationContext context)
    {
        EvaluateCount++;
        CapturedContext = context;
        return Task.FromResult(_predicate(context));
    }
}
