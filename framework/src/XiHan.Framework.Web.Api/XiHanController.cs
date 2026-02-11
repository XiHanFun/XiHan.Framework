#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanController
// Guid:43b9cb63-7a79-4772-9e93-a8e2abc16fdd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/13 01:51:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Core.Aspects;
using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Web.Api;

/// <summary>
/// XiHanController
/// </summary>
public abstract class XiHanController : Controller, IAvoidDuplicateCrossCuttingConcerns
{
    /// <summary>
    /// 缓存服务提供程序
    /// </summary>
    public ICachedServiceProvider CachedServiceProvider { get; set; } = null!;

    /// <summary>
    /// 应用的横切关注点
    /// </summary>
    public List<string> AppliedCrossCuttingConcerns => throw new NotImplementedException();

    /// <summary>
    /// 返回统一成功响应（含数据）
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="data">返回数据</param>
    /// <param name="message">提示信息</param>
    /// <returns></returns>
    protected virtual IActionResult Success<T>(T? data, string message = "操作成功")
        => Ok(ApiResponse<T>.Ok(data, message, HttpContext.TraceIdentifier));

    /// <summary>
    /// 返回统一成功响应（无数据）
    /// </summary>
    /// <param name="message">提示信息</param>
    /// <returns></returns>
    protected virtual IActionResult Success(string message = "操作成功")
        => Ok(ApiResponse.Ok(message, HttpContext.TraceIdentifier));

    /// <summary>
    /// 返回统一失败响应
    /// </summary>
    /// <param name="message">错误信息</param>
    /// <param name="code">业务码</param>
    /// <param name="statusCode">HTTP 状态码</param>
    /// <returns></returns>
    protected virtual IActionResult Fail(
        string message = "操作失败",
        ApiResponseCodes code = ApiResponseCodes.Failed,
        int statusCode = StatusCodes.Status400BadRequest)
        => StatusCode(statusCode, ApiResponse.Fail(message, code, HttpContext.TraceIdentifier));

    /// <summary>
    /// 返回统一失败响应（含数据）
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="message">错误信息</param>
    /// <param name="data">可选数据</param>
    /// <param name="code">业务码</param>
    /// <param name="statusCode">HTTP 状态码</param>
    /// <returns></returns>
    protected virtual IActionResult Fail<T>(
        string message,
        T? data,
        ApiResponseCodes code = ApiResponseCodes.Failed,
        int statusCode = StatusCodes.Status400BadRequest)
        => StatusCode(statusCode, ApiResponse<T>.Fail(message, code, data, HttpContext.TraceIdentifier));
}
