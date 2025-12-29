#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDynamicApiConvention
// Guid:g7h8i9j0-k1l2-4m3n-4o5p-6q7r8s9t0u1v
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.Conventions;

/// <summary>
/// 动态 API 约定接口
/// </summary>
public interface IDynamicApiConvention
{
    /// <summary>
    /// 应用约定
    /// </summary>
    /// <param name="context">约定上下文</param>
    void Apply(DynamicApiConventionContext context);
}
