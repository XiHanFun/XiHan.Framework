#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanConsoleLoggerProvider
// Guid:f7ece3ee-eb6d-4913-82ac-bc3037ec284b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 12:25:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Logging.Options;

namespace XiHan.Framework.Logging.Providers;

/// <summary>
/// XiHan 控制台日志提供器
/// </summary>
[ProviderAlias("XiHanConsole")]
public class XiHanConsoleLoggerProvider : ILoggerProvider
{
    private readonly XiHanConsoleLoggerOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">控制台日志选项</param>
    public XiHanConsoleLoggerProvider(IOptions<XiHanConsoleLoggerOptions> options)
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
        return new XiHanConsoleLogger(categoryName, _options);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        // 清理资源
    }
}
