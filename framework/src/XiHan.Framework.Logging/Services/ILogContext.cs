// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// 日志上下文接口
/// </summary>
public interface ILogContext
{
    /// <summary>
    /// 用户唯一标识
    /// </summary>
    string? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    string? UserName { get; set; }

    /// <summary>
    /// 租户唯一标识
    /// </summary>
    string? TenantId { get; set; }

    /// <summary>
    /// 请求唯一标识
    /// </summary>
    string? RequestId { get; set; }

    /// <summary>
    /// 跟踪唯一标识
    /// </summary>
    string? TraceId { get; set; }

    /// <summary>
    /// 会话唯一标识
    /// </summary>
    string? SessionId { get; set; }

    /// <summary>
    /// IP 地址
    /// </summary>
    string? IpAddress { get; set; }

    /// <summary>
    /// User Agent
    /// </summary>
    string? UserAgent { get; set; }

    /// <summary>
    /// 自定义属性
    /// </summary>
    Dictionary<string, object> Properties { get; }

    /// <summary>
    /// 设置属性
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    void SetProperty(string key, object value);

    /// <summary>
    /// 获取属性
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="key">键</param>
    /// <returns></returns>
    T? GetProperty<T>(string key);

    /// <summary>
    /// 移除属性
    /// </summary>
    /// <param name="key">键</param>
    /// <returns></returns>
    bool RemoveProperty(string key);

    /// <summary>
    /// 清除所有属性
    /// </summary>
    void Clear();

    /// <summary>
    /// 创建属性作用域
    /// </summary>
    /// <param name="properties">属性</param>
    /// <returns></returns>
    IDisposable CreateScope(Dictionary<string, object> properties);

    /// <summary>
    /// 创建属性作用域
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    IDisposable CreateScope(string key, object value);
}
