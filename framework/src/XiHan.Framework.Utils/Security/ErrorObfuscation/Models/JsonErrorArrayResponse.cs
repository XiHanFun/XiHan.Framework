// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
