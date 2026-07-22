// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Providers;

/// <summary>
/// XiHan 文件日志提供器
/// </summary>
[ProviderAlias("XiHanFile")]
public class XiHanFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly XiHanFileLoggerOptions _options;
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">文件日志选项</param>
    public XiHanFileLoggerProvider(IOptions<XiHanFileLoggerOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// 创建日志器
    /// </summary>
    /// <param name="categoryName">分类名称</param>
    /// <returns></returns>
    public ILogger CreateLogger(string categoryName)
    {
        return new XiHanFileLogger(categoryName, _options, _scopeProvider);
    }

    /// <summary>
    /// 设置外部作用域提供器
    /// </summary>
    /// <param name="scopeProvider">作用域提供器</param>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 清理资源
    }
}
