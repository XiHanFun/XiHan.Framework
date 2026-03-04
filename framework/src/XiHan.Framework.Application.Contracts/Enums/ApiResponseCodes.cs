#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApiResponseCodes
// Guid:5ad3b310-4347-4d6e-b9e4-8271db55e01e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;

namespace XiHan.Framework.Application.Contracts.Enums;

/// <summary>
/// 统一返回码（与常见 HTTP 状态码对齐，便于前后端统一理解）
/// </summary>
public enum ApiResponseCodes
{
    /// <summary>
    /// 继续请求
    /// </summary>
    [Description("继续请求")]
    Continue = 100,

    /// <summary>
    /// 切换协议
    /// </summary>
    [Description("切换协议")]
    SwitchingProtocols = 101,

    /// <summary>
    /// 请求成功
    /// </summary>
    [Description("请求成功")]
    Success = 200,

    /// <summary>
    /// 等待响应
    /// </summary>
    [Description("等待响应")]
    Created = 201,

    /// <summary>
    /// 返回多条重定向供选择
    /// </summary>
    [Description("返回多条重定向供选择")]
    MultipleChoices = 300,

    /// <summary>
    /// 永久重定向
    /// </summary>
    [Description("永久重定向")]
    MovedPermanently = 301,

    /// <summary>
    /// 请求错误
    /// </summary>
    [Description("请求错误")]
    BadRequest = 400,

    /// <summary>
    /// 未授权
    /// </summary>
    [Description("未授权")]
    Unauthorized = 401,

    /// <summary>
    /// 禁止访问
    /// </summary>
    [Description("禁止访问")]
    Forbidden = 403,

    /// <summary>
    /// 资源不存在
    /// </summary>
    [Description("资源不存在")]
    NotFound = 404,

    /// <summary>
    /// 请求方法不允许
    /// </summary>
    [Description("请求方法不允许")]
    MethodNotAllowed = 405,

    /// <summary>
    /// 请求超时
    /// </summary>
    [Description("请求超时")]
    RequestTimeOut = 408,

    /// <summary>
    /// 请求的语义错误
    /// </summary>
    [Description("请求的语义错误")]
    UnprocessableEntity = 422,

    /// <summary>
    /// 并发请求过多
    /// </summary>
    [Description("并发请求过多")]
    TooManyRequests = 429,

    /// <summary>
    /// 服务器内部错误
    /// </summary>
    [Description("服务器内部错误")]
    Failed = 500,

    /// <summary>
    /// 服务不可用
    /// </summary>
    [Description("服务不可用")]
    ServiceUnavailable = 501
}
