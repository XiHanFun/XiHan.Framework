#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanLoggerBuilder
// Guid:cd0ca5ff-940a-41a6-8296-e37433e984da
// Author:afand
// Email:me@zhaifanhua.com
// CreateTime:2025/2/21 15:55:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Serilog.Core;

namespace XiHan.Framework.AspNetCore.Serilog;

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
