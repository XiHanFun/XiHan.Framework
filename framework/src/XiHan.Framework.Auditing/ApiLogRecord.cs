#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ApiLogRecord
// Guid:c4d5e6f7-8901-2345-6789-0abcdef12345
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Auditing;

/// <summary>
/// 接口日志记录模型
/// </summary>
public class ApiLogRecord
{
    /// <summary>
    /// 跟踪标识
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 用户标识
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 客户端标识（AccessKey）
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// 应用标识
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// 签名是否有效
    /// </summary>
    public bool IsSignatureValid { get; set; } = true;

    /// <summary>
    /// 签名算法
    /// </summary>
    public string? SignatureAlgorithm { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// API 名称（端点显示名，Minimal API 亦有值，作为路径之外的可读标识）
    /// </summary>
    public string? ApiName { get; set; }

    /// <summary>
    /// 控制器
    /// </summary>
    public string? ControllerName { get; set; }

    /// <summary>
    /// 动作
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// 请求参数（JSON）
    /// </summary>
    public string? RequestParams { get; set; }

    /// <summary>
    /// 请求体
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// 响应体
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// 状态码
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 远端 IP
    /// </summary>
    public string? RemoteIp { get; set; }

    /// <summary>
    /// UserAgent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 来源地址
    /// </summary>
    public string? Referer { get; set; }

    /// <summary>
    /// 耗时毫秒
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 请求大小（字节）
    /// </summary>
    public long RequestSize { get; set; }

    /// <summary>
    /// 响应大小（字节）
    /// </summary>
    public long ResponseSize { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
