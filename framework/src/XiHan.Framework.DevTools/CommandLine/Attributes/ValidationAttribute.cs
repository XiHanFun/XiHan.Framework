#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ValidationAttribute
// Guid:d0c354b7-da79-4069-a628-d28444cb8ff0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:02:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Attributes;

/// <summary>
/// 验证属性标记
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class ValidationAttribute : Attribute
{
    /// <summary>
    /// 创建验证属性
    /// </summary>
    /// <param name="validatorType">验证器类型</param>
    public ValidationAttribute(Type validatorType)
    {
        ValidatorType = validatorType ?? throw new ArgumentNullException(nameof(validatorType));
    }

    /// <summary>
    /// 验证器类型
    /// </summary>
    public Type ValidatorType { get; }

    /// <summary>
    /// 验证参数
    /// </summary>
    public object[]? Parameters { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
