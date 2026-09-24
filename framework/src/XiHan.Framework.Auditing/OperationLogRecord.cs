// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Auditing;

/// <summary>
/// 操作日志记录模型
/// </summary>
public class OperationLogRecord
{
    /// <summary>
    /// 跟踪标识
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// 会话标识
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// 用户标识
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 租户标识（记录产生时所属的租户；null 表示宿主/平台）
    /// </summary>
    /// <remarks>写入器按它落戳并切入该租户写入，不依赖写入时的环境上下文——排队异步写入时环境里已没有请求的租户。</remarks>
    public long? TenantId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 控制器
    /// </summary>
    public string? ControllerName { get; set; }

    /// <summary>
    /// 动作
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 请求路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 请求参数（JSON）
    /// </summary>
    public string? RequestParams { get; set; }

    /// <summary>
    /// 响应结果（JSON）
    /// </summary>
    public string? ResponseResult { get; set; }

    /// <summary>
    /// 状态码
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 耗时毫秒
    /// </summary>
    public long ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 远端 IP
    /// </summary>
    public string? RemoteIp { get; set; }

    /// <summary>
    /// UserAgent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 异常消息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
