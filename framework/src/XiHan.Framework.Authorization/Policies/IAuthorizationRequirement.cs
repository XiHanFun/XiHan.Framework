#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAuthorizationRequirement
// Guid:b6c7d8e9-f0a1-2345-9012-123456789035
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
