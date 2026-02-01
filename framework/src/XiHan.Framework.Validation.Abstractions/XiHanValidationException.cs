#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanValidationException
// Guid:6b6e79dc-f028-4f7d-a070-617d13a14ae8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 06:46:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.Core.Extensions.Logging;
using XiHan.Framework.Core.Logging;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Validation.Abstractions;

/// <summary>
/// 曦寒验证异常类
/// 用于表示数据验证失败时抛出的异常，包含详细的验证错误信息和日志记录功能
/// </summary>
public class XiHanValidationException : XiHanException,
    IHasLogLevel,
    IHasValidationErrors,
    IExceptionWithSelfLogging
{
    /// <summary>
    /// 初始化曦寒验证异常的新实例
    /// </summary>
    public XiHanValidationException()
    {
        ValidationErrors = [];
        LogLevel = LogLevel.Warning;
    }

    /// <summary>
    /// 使用指定的错误消息初始化曦寒验证异常的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    public XiHanValidationException(string message)
        : base(message)
    {
        ValidationErrors = [];
        LogLevel = LogLevel.Warning;
    }

    /// <summary>
    /// 使用指定的验证错误集合初始化曦寒验证异常的新实例
    /// </summary>
    /// <param name="validationErrors">验证错误集合</param>
    public XiHanValidationException(IList<ValidationResult> validationErrors)
    {
        ValidationErrors = validationErrors;
        LogLevel = LogLevel.Warning;
    }

    /// <summary>
    /// 使用指定的错误消息和验证错误集合初始化曦寒验证异常的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="validationErrors">验证错误集合</param>
    public XiHanValidationException(string message, IList<ValidationResult> validationErrors)
        : base(message)
    {
        ValidationErrors = validationErrors;
        LogLevel = LogLevel.Warning;
    }

    /// <summary>
    /// 使用指定的错误消息和内部异常初始化曦寒验证异常的新实例
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public XiHanValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationErrors = [];
        LogLevel = LogLevel.Warning;
    }

    /// <summary>
    /// 获取此异常的详细验证错误列表
    /// </summary>
    public IList<ValidationResult> ValidationErrors { get; }

    /// <summary>
    /// 获取或设置异常的严重性级别
    /// 默认值：Warning（警告）
    /// </summary>
    public LogLevel LogLevel { get; set; }

    /// <summary>
    /// 使用指定的日志记录器记录验证错误信息
    /// </summary>
    /// <param name="logger">日志记录器实例</param>
    /// <exception cref="ArgumentNullException">当 logger 为 null 时</exception>
    public void Log(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (ValidationErrors.IsNullOrEmpty())
        {
            return;
        }

        var validationErrors = new StringBuilder();
        validationErrors.AppendLine($"存在 {ValidationErrors.Count} 个验证错误：");
        foreach (var validationResult in ValidationErrors)
        {
            var memberNames = "";
            if (validationResult.MemberNames.Any())
            {
                memberNames = " (" + string.Join(", ", validationResult.MemberNames) + ")";
            }

            validationErrors.AppendLine(validationResult.ErrorMessage + memberNames);
        }

        logger.LogWithLevel(LogLevel, validationErrors.ToString());
    }
}
