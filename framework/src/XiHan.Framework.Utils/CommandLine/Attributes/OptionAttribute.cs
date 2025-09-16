#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OptionAttribute
// Guid:f5b3d147-8c4e-4d29-9a2f-12fd84c18139
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/16 0:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.CommandLine.Attributes;

/// <summary>
/// 命令行选项属性标记
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class OptionAttribute : Attribute
{
    /// <summary>
    /// 创建选项属性
    /// </summary>
    /// <param name="longName">长选项名称</param>
    public OptionAttribute(string longName)
    {
        LongName = longName ?? throw new ArgumentNullException(nameof(longName));
    }

    /// <summary>
    /// 创建选项属性
    /// </summary>
    /// <param name="longName">长选项名称</param>
    /// <param name="shortName">短选项名称</param>
    public OptionAttribute(string longName, string shortName) : this(longName)
    {
        ShortName = shortName;
    }

    /// <summary>
    /// 长选项名称
    /// </summary>
    public string LongName { get; }

    /// <summary>
    /// 短选项名称
    /// </summary>
    public string? ShortName { get; set; }

    /// <summary>
    /// 选项描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 是否为布尔开关
    /// </summary>
    public bool IsSwitch { get; set; }

    /// <summary>
    /// 是否支持多值
    /// </summary>
    public bool AllowMultiple { get; set; }

    /// <summary>
    /// 值分隔符（用于多值选项）
    /// </summary>
    public char Separator { get; set; } = ',';

    /// <summary>
    /// 参数名称（用于帮助显示）
    /// </summary>
    public string? MetaName { get; set; }
}

/// <summary>
/// 命令行参数属性标记（位置参数）
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ArgumentAttribute : Attribute
{
    /// <summary>
    /// 创建参数属性
    /// </summary>
    /// <param name="position">参数位置</param>
    /// <param name="name">参数名称</param>
    public ArgumentAttribute(int position, string name)
    {
        Position = position;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 参数位置（从0开始）
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 参数描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否必填
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 是否支持多值（通常用于最后一个参数）
    /// </summary>
    public bool AllowMultiple { get; set; }
}

/// <summary>
/// 命令属性标记
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CommandAttribute : Attribute
{
    /// <summary>
    /// 创建命令属性
    /// </summary>
    /// <param name="name">命令名称</param>
    public CommandAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// 命令名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 命令别名
    /// </summary>
    public string[]? Aliases { get; set; }

    /// <summary>
    /// 命令描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否为默认命令
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 是否隐藏（不在帮助中显示）
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// 使用示例
    /// </summary>
    public string? Usage { get; set; }
}

/// <summary>
/// 子命令属性标记
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class SubCommandAttribute : Attribute
{
    /// <summary>
    /// 创建子命令属性
    /// </summary>
    /// <param name="commandType">子命令类型</param>
    public SubCommandAttribute(Type commandType)
    {
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
    }

    /// <summary>
    /// 子命令类型
    /// </summary>
    public Type CommandType { get; }
}

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

/// <summary>
/// 范围验证属性
/// </summary>
public class RangeAttribute : ValidationAttribute
{
    /// <summary>
    /// 创建范围验证属性
    /// </summary>
    /// <param name="minimum">最小值</param>
    /// <param name="maximum">最大值</param>
    public RangeAttribute(object minimum, object maximum) : base(typeof(RangeValidator))
    {
        Minimum = minimum;
        Maximum = maximum;
        Parameters = [minimum, maximum];
    }

    /// <summary>
    /// 最小值
    /// </summary>
    public object Minimum { get; }

    /// <summary>
    /// 最大值
    /// </summary>
    public object Maximum { get; }
}

/// <summary>
/// 文件存在验证属性
/// </summary>
public class FileExistsAttribute : ValidationAttribute
{
    /// <summary>
    /// 创建文件存在验证属性
    /// </summary>
    public FileExistsAttribute() : base(typeof(FileExistsValidator))
    {
    }
}

/// <summary>
/// 目录存在验证属性
/// </summary>
public class DirectoryExistsAttribute : ValidationAttribute
{
    /// <summary>
    /// 创建目录存在验证属性
    /// </summary>
    public DirectoryExistsAttribute() : base(typeof(DirectoryExistsValidator))
    {
    }
}

#region 验证器实现

/// <summary>
/// 验证器接口
/// </summary>
public interface IValidator
{
    /// <summary>
    /// 验证值
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameters">验证参数</param>
    /// <returns>验证结果</returns>
    ValidationResult Validate(object? value, object[]? parameters = null);
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 创建验证结果
    /// </summary>
    /// <param name="isValid">是否有效</param>
    /// <param name="errorMessage">错误消息</param>
    public ValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 成功结果
    /// </summary>
    public static ValidationResult Success => new(true);

    /// <summary>
    /// 是否验证成功
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// 创建错误结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <returns>验证结果</returns>
    public static ValidationResult Error(string errorMessage)
    {
        return new ValidationResult(false, errorMessage);
    }
}

/// <summary>
/// 范围验证器
/// </summary>
public class RangeValidator : IValidator
{
    /// <summary>
    /// 验证值是否在指定范围内
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameters">验证参数 [minimum, maximum]</param>
    /// <returns>验证结果</returns>
    public ValidationResult Validate(object? value, object[]? parameters = null)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (parameters == null || parameters.Length < 2)
        {
            return ValidationResult.Error("范围验证需要最小值和最大值参数");
        }

        try
        {
            if (value is not IComparable comparable)
            {
                return ValidationResult.Error("值不支持比较操作");
            }

            if (parameters[0] is IComparable minimum && comparable.CompareTo(minimum) < 0)
            {
                return ValidationResult.Error($"值 {value} 小于最小值 {minimum}");
            }

            if (parameters[1] is IComparable maximum && comparable.CompareTo(maximum) > 0)
            {
                return ValidationResult.Error($"值 {value} 大于最大值 {maximum}");
            }

            return ValidationResult.Success;
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"范围验证失败: {ex.Message}");
        }
    }
}

/// <summary>
/// 文件存在验证器
/// </summary>
public class FileExistsValidator : IValidator
{
    /// <summary>
    /// 验证文件是否存在
    /// </summary>
    /// <param name="value">文件路径</param>
    /// <param name="parameters">验证参数（未使用）</param>
    /// <returns>验证结果</returns>
    public ValidationResult Validate(object? value, object[]? parameters = null)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        var filePath = value.ToString();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ValidationResult.Error("文件路径不能为空");
        }

        if (!File.Exists(filePath))
        {
            return ValidationResult.Error($"文件 '{filePath}' 不存在");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// 目录存在验证器
/// </summary>
public class DirectoryExistsValidator : IValidator
{
    /// <summary>
    /// 验证目录是否存在
    /// </summary>
    /// <param name="value">目录路径</param>
    /// <param name="parameters">验证参数（未使用）</param>
    /// <returns>验证结果</returns>
    public ValidationResult Validate(object? value, object[]? parameters = null)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        var directoryPath = value.ToString();
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return ValidationResult.Error("目录路径不能为空");
        }

        if (!Directory.Exists(directoryPath))
        {
            return ValidationResult.Error($"目录 '{directoryPath}' 不存在");
        }

        return ValidationResult.Success;
    }
}

#endregion
