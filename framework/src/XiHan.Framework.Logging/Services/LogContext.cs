#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LogContext
// Guid:c6756496-36f6-4a7c-a9dc-8b49feb42135
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 12:15:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;

namespace XiHan.Framework.Logging.Services;

/// <summary>
/// 日志上下文实现
/// </summary>
public class LogContext : ILogContext
{
    private readonly ConcurrentDictionary<string, object> _properties = new();

    /// <summary>
    /// 用户唯一标识
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 请求唯一标识
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// 跟踪唯一标识
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// 会话唯一标识
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// IP 地址
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 自定义属性
    /// </summary>
    public Dictionary<string, object> Properties => new(_properties);

    /// <summary>
    /// 设置属性
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public void SetProperty(string key, object value)
    {
        _properties.AddOrUpdate(key, value, (_, _) => value);
    }

    /// <summary>
    /// 获取属性
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="key">键</param>
    /// <returns></returns>
    public T? GetProperty<T>(string key)
    {
        if (_properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// 移除属性
    /// </summary>
    /// <param name="key">键</param>
    /// <returns></returns>
    public bool RemoveProperty(string key)
    {
        return _properties.TryRemove(key, out _);
    }

    /// <summary>
    /// 清除所有属性
    /// </summary>
    public void Clear()
    {
        _properties.Clear();
        UserId = null;
        UserName = null;
        TenantId = null;
        RequestId = null;
        TraceId = null;
        SessionId = null;
        IpAddress = null;
        UserAgent = null;
    }

    /// <summary>
    /// 创建属性作用域
    /// </summary>
    /// <param name="properties">属性</param>
    /// <returns></returns>
    public IDisposable CreateScope(Dictionary<string, object> properties)
    {
        return new LogContextScope(this, properties);
    }

    /// <summary>
    /// 创建属性作用域
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <returns></returns>
    public IDisposable CreateScope(string key, object value)
    {
        return new LogContextScope(this, new Dictionary<string, object> { { key, value } });
    }
}

/// <summary>
/// 日志上下文作用域
/// </summary>
internal class LogContextScope : IDisposable
{
    private readonly LogContext _context;
    private readonly Dictionary<string, object> _originalValues = [];
    private readonly List<string> _addedKeys = [];
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="context">日志上下文</param>
    /// <param name="properties">属性字典</param>
    public LogContextScope(LogContext context, Dictionary<string, object> properties)
    {
        _context = context;

        foreach (var (key, value) in properties)
        {
            if (_context.Properties.TryGetValue(key, out var originalValue))
            {
                _originalValues[key] = originalValue;
            }
            else
            {
                _addedKeys.Add(key);
            }

            _context.SetProperty(key, value);
        }
    }

    /// <summary>
    /// 释放资源，恢复原始上下文状态
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // 恢复原始值
        foreach (var (key, value) in _originalValues)
        {
            _context.SetProperty(key, value);
        }

        // 移除新添加的键
        foreach (var key in _addedKeys)
        {
            _context.RemoveProperty(key);
        }

        _disposed = true;
    }
}
