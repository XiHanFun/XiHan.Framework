// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Serilog.Core;

namespace XiHan.Framework.Logging;

/// <summary>
/// 曦寒日志构建器
/// </summary>
public class XiHanLoggerBuilder
{
    private readonly XiHanLoggerConfigurationBuilder _loggerConfiguration;

    /// <summary>
    /// 构造函数
    /// </summary>
    public XiHanLoggerBuilder()
    {
        _loggerConfiguration = new XiHanLoggerConfigurationBuilder();
    }

    /// <summary>
    /// 创建日志记录器
    /// </summary>
    /// <returns></returns>
    public Logger CreateLogger(IConfiguration configuration)
    {
        return _loggerConfiguration.Build(configuration).CreateLogger();
    }

    /// <summary>
    /// 创建日志记录器
    /// </summary>
    /// <returns></returns>
    public Logger CreateLogger()
    {
        return _loggerConfiguration.Build().CreateLogger();
    }

    /// <summary>
    /// 创建默认日志记录器
    /// </summary>
    /// <returns></returns>
    public Logger CreateLoggerDefault()
    {
        return _loggerConfiguration.BuildDefault().CreateLogger();
    }
}
