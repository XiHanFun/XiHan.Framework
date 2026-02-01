#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFileLoggerProvider
// Guid:69fffdc3-37cd-4794-8a16-d696883daa88
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 12:20:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Providers;

/// <summary>
/// XiHan 文件日志提供器
/// </summary>
[ProviderAlias("XiHanFile")]
public class XiHanFileLoggerProvider : ILoggerProvider
{
    private readonly XiHanFileLoggerOptions _options;

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
        return new XiHanFileLogger(categoryName, _options);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 清理资源
    }
}
