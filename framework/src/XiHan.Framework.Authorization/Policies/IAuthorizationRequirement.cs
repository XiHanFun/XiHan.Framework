// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Policies;

/// <summary>
/// 授权要求接口
/// </summary>
/// <remarks>
/// 用于定义自定义授权要求
/// </remarks>
public interface IAuthorizationRequirement
{
    /// <summary>
    /// 要求名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 评估授权要求
    /// </summary>
    /// <param name="context">授权上下文</param>
    /// <returns>是否满足要求</returns>
    Task<bool> EvaluateAsync(AuthorizationContext context);
}
