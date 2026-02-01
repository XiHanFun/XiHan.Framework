#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:JsonErrorArrayResponse
// Guid:8d9e0f1a-3b4c-5d6e-7f8a-9b0c1d2e3f4a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Models;

/// <summary>
/// JSON 错误数组响应对象
/// </summary>
public class JsonErrorArrayResponse
{
    /// <summary>
    /// 错误列表
    /// </summary>
    public List<ErrorItem> Errors { get; set; } = [];

    /// <summary>
    /// ISO 格式时间戳
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// 追踪ID
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 错误数量
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// 错误项
/// </summary>
public class ErrorItem
{
    /// <summary>
    /// 错误代码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 错误类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 详细信息
    /// </summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// 来源信息
    /// </summary>
    public ErrorSource? Source { get; set; }
}

/// <summary>
/// 错误来源
/// </summary>
public class ErrorSource
{
    /// <summary>
    /// 编程语言
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 异常类型
    /// </summary>
    public string Exception { get; set; } = string.Empty;
}
