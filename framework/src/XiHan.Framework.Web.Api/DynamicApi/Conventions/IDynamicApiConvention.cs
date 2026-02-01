#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDynamicApiConvention
// Guid:5d3a8fca-0d71-4b57-b81a-c1498de3d92b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 00:00:00
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
