#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParameterKind
// Guid:param-kind-dynamic-api-2025
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 参数物理类型分类
/// </summary>
public enum ParameterKind
{
    /// <summary>
    /// 路由键参数（如 id, xxxId）
    /// </summary>
    RouteKey,

    /// <summary>
    /// 简单类型（int, string, bool, DateTime 等）
    /// </summary>
    Simple,

    /// <summary>
    /// 复杂类型（class, record）
    /// </summary>
    Complex,

    /// <summary>
    /// 集合类型（IEnumerable&lt;T&gt;）
    /// </summary>
    Collection,

    /// <summary>
    /// 特殊类型（CancellationToken, HttpContext 等）
    /// </summary>
    Special
}
