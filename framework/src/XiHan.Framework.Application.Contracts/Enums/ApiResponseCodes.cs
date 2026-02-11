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

namespace XiHan.Framework.Application.Contracts.Enums;

/// <summary>
/// 统一返回码（与常见 HTTP 状态码对齐，便于前后端统一理解）
/// </summary>
public enum ApiResponseCodes
{
    /// <summary>
    /// 成功（常见约定，与 HTTP 200 一致）
    /// </summary>
    Success = 200,

    /// <summary>
    /// 请求参数错误 / 非法请求
    /// </summary>
    BadRequest = 400,

    /// <summary>
    /// 未授权（未登录或 Token 无效）
    /// </summary>
    Unauthorized = 401,

    /// <summary>
    /// 禁止访问（无权限）
    /// </summary>
    Forbidden = 403,

    /// <summary>
    /// 资源不存在
    /// </summary>
    NotFound = 404,

    /// <summary>
    /// 请求方法不允许
    /// </summary>
    MethodNotAllowed = 405,

    /// <summary>
    /// 请求超时
    /// </summary>
    RequestTimeout = 408,

    /// <summary>
    /// 业务失败 / 服务器内部错误（常见约定，与 HTTP 500 一致）
    /// </summary>
    Failed = 500,

    /// <summary>
    /// 服务不可用
    /// </summary>
    ServiceUnavailable = 503
}
