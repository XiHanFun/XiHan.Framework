// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
