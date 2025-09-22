#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateValidationResult
// Guid:1a3b56e0-6fd7-4866-80ab-0039dec5b82c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 3:51:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Engines;

/// <summary>
/// 模板验证结果
/// </summary>
public record TemplateValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 错误位置（行号）
    /// </summary>
    public int? ErrorLine { get; init; }

    /// <summary>
    /// 错误位置（列号）
    /// </summary>
    public int? ErrorColumn { get; init; }

    /// <summary>
    /// 成功验证结果
    /// </summary>
    public static TemplateValidationResult Success => new() { IsValid = true };

    /// <summary>
    /// 创建失败验证结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <param name="line">错误行号</param>
    /// <param name="column">错误列号</param>
    /// <returns>验证结果</returns>
    public static TemplateValidationResult Failure(string errorMessage, int? line = null, int? column = null)
        => new() { IsValid = false, ErrorMessage = errorMessage, ErrorLine = line, ErrorColumn = column };
}
