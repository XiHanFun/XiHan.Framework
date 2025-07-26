#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkLogAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Enums;

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架日志特性
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class FrameworkLogAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <param name="logMessage">日志消息</param>
    /// <param name="logParameters">是否记录参数</param>
    /// <param name="logReturnValue">是否记录返回值</param>
    public FrameworkLogAttribute(LogLevel logLevel = LogLevel.Information, string logMessage = "", bool logParameters = false, bool logReturnValue = false)
    {
        LogLevel = logLevel;
        LogMessage = logMessage;
        LogParameters = logParameters;
        LogReturnValue = logReturnValue;
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public LogLevel LogLevel { get; }

    /// <summary>
    /// 日志消息
    /// </summary>
    public string LogMessage { get; }

    /// <summary>
    /// 是否记录参数
    /// </summary>
    public bool LogParameters { get; }

    /// <summary>
    /// 是否记录返回值
    /// </summary>
    public bool LogReturnValue { get; }
}
