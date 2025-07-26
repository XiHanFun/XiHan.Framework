#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkValidationAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架验证特性
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class FrameworkValidationAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="validationRule">验证规则</param>
    /// <param name="errorMessage">错误消息</param>
    public FrameworkValidationAttribute(string validationRule, string errorMessage = "")
    {
        ValidationRule = validationRule;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 验证规则
    /// </summary>
    public string ValidationRule { get; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; }
}
