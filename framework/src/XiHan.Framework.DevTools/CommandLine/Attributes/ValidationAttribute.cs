// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
