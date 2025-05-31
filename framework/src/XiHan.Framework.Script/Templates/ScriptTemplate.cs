#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptTemplate
// Guid:e1f2g3h4-i5j6-k7l8-m9n0-o1p2q3r4s5t6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/2 10:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Templates;

/// <summary>
/// 模板参数类型
/// </summary>
public enum TemplateParameterType
{
    /// <summary>
    /// 字符串
    /// </summary>
    String,

    /// <summary>
    /// 整数
    /// </summary>
    Integer,

    /// <summary>
    /// 双精度浮点数
    /// </summary>
    Double,

    /// <summary>
    /// 布尔值
    /// </summary>
    Boolean,

    /// <summary>
    /// 枚举
    /// </summary>
    Enum
}

/// <summary>
/// 脚本模板
/// </summary>
public class ScriptTemplate
{
    /// <summary>
    /// 模板名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模板描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 模板分类
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// 模板作者
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 模板版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 模板代码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 模板参数
    /// </summary>
    public List<TemplateParameter> Parameters { get; set; } = [];

    /// <summary>
    /// 所需的引用
    /// </summary>
    public List<string> RequiredReferences { get; set; } = [];

    /// <summary>
    /// 所需的命名空间
    /// </summary>
    public List<string> RequiredNamespaces { get; set; } = [];

    /// <summary>
    /// 标签
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 是否为系统模板
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// 模板使用示例
    /// </summary>
    public string? Example { get; set; }

    /// <summary>
    /// 生成脚本代码
    /// </summary>
    /// <param name="parameters">参数值</param>
    /// <returns>生成的脚本代码</returns>
    public string GenerateCode(Dictionary<string, object?> parameters)
    {
        var code = Code;

        foreach (var parameter in Parameters)
        {
            var value = parameters.TryGetValue(parameter.Name, out var paramValue)
                ? paramValue?.ToString() ?? parameter.DefaultValue
                : parameter.DefaultValue;

            code = code.Replace($"#{{{parameter.Name}}}", value);
        }

        return code;
    }

    /// <summary>
    /// 验证参数
    /// </summary>
    /// <param name="parameters">参数值</param>
    /// <returns>验证结果</returns>
    public TemplateValidationResult ValidateParameters(Dictionary<string, object?> parameters)
    {
        var errors = new List<string>();

        foreach (var parameter in Parameters.Where(p => p.Required))
        {
            if (!parameters.TryGetValue(parameter.Name, out var value) || value == null ||
                string.IsNullOrWhiteSpace(value?.ToString()))
            {
                errors.Add($"必需参数 '{parameter.Name}' 未提供");
            }
        }

        foreach (var kvp in parameters)
        {
            var parameter = Parameters.FirstOrDefault(p => p.Name == kvp.Key);
            if (parameter != null)
            {
                var validationError = parameter.ValidateValue(kvp.Value);
                if (!string.IsNullOrEmpty(validationError))
                {
                    errors.Add(validationError);
                }
            }
        }

        return new TemplateValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}

/// <summary>
/// 模板参数
/// </summary>
public class TemplateParameter
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 参数类型
    /// </summary>
    public TemplateParameterType Type { get; set; } = TemplateParameterType.String;

    /// <summary>
    /// 参数描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 默认值
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// 可选值列表（用于枚举类型）
    /// </summary>
    public List<string> Options { get; set; } = [];

    /// <summary>
    /// 最小值（用于数值类型）
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// 最大值（用于数值类型）
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// 正则表达式验证（用于字符串类型）
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// 验证参数值
    /// </summary>
    /// <param name="value">参数值</param>
    /// <returns>验证错误信息，如果验证成功则返回null</returns>
    public string? ValidateValue(object? value)
    {
        if (value == null)
        {
            return Required ? $"参数 '{Name}' 是必需的" : null;
        }

        var stringValue = value.ToString();
        return string.IsNullOrEmpty(stringValue)
            ? Required ? $"参数 '{Name}' 不能为空" : null
            : Type switch
            {
                TemplateParameterType.Integer => ValidateInteger(stringValue),
                TemplateParameterType.Double => ValidateDouble(stringValue),
                TemplateParameterType.Boolean => ValidateBoolean(stringValue),
                TemplateParameterType.Enum => ValidateEnum(stringValue),
                TemplateParameterType.String => ValidateString(stringValue),
                _ => null
            };
    }

    private string? ValidateInteger(string value)
    {
        return !int.TryParse(value, out var intValue)
            ? $"参数 '{Name}' 必须是整数"
            : MinValue.HasValue && intValue < MinValue.Value
            ? $"参数 '{Name}' 必须大于等于 {MinValue.Value}"
            : MaxValue.HasValue && intValue > MaxValue.Value ? $"参数 '{Name}' 必须小于等于 {MaxValue.Value}" : null;
    }

    private string? ValidateDouble(string value)
    {
        return !double.TryParse(value, out var doubleValue)
            ? $"参数 '{Name}' 必须是数字"
            : MinValue.HasValue && doubleValue < MinValue.Value
            ? $"参数 '{Name}' 必须大于等于 {MinValue.Value}"
            : MaxValue.HasValue && doubleValue > MaxValue.Value ? $"参数 '{Name}' 必须小于等于 {MaxValue.Value}" : null;
    }

    private string? ValidateBoolean(string value)
    {
        return !bool.TryParse(value, out _) ? $"参数 '{Name}' 必须是布尔值 (true/false)" : null;
    }

    private string? ValidateEnum(string value)
    {
        return Options.Count > 0 && !Options.Contains(value) ? $"参数 '{Name}' 必须是以下值之一: {string.Join(", ", Options)}" : null;
    }

    private string? ValidateString(string value)
    {
        if (!string.IsNullOrEmpty(Pattern))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, Pattern))
            {
                return $"参数 '{Name}' 格式不正确";
            }
        }

        return null;
    }
}

/// <summary>
/// 模板验证结果
/// </summary>
public class TemplateValidationResult
{
    /// <summary>
    /// 是否验证成功
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// 格式化错误信息
    /// </summary>
    /// <returns>格式化的错误信息</returns>
    public string FormatErrors()
    {
        return string.Join(Environment.NewLine, Errors);
    }
}
