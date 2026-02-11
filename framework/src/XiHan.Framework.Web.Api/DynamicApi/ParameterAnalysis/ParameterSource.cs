#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ParameterSource
// Guid:82dc6cdb-aa42-47a9-bb4b-7a382b3f0345
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/12/30 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Web.Api.DynamicApi.ParameterAnalysis;

/// <summary>
/// 参数来源
/// </summary>
public enum ParameterSource
{
    /// <summary>
    /// 从路由获取
    /// </summary>
    Route,

    /// <summary>
    /// 从查询字符串获取
    /// </summary>
    Query,

    /// <summary>
    /// 从请求体获取
    /// </summary>
    Body,

    /// <summary>
    /// 从服务容器获取
    /// </summary>
    Services,

    /// <summary>
    /// 从请求头获取
    /// </summary>
    Header,

    /// <summary>
    /// 从表单获取
    /// </summary>
    Form
}
