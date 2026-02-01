#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateParameter
// Guid:39f83115-82bf-4039-8c95-f9240cab15ff
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/02 08:09:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Templates;

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
    /// 可选值列表(用于枚举类型)
    /// </summary>
    public List<string> Options { get; set; } = [];

    /// <summary>
    /// 最小值(用于数值类型)
    /// </summary>
    public double? MinValue { get; set; }

    /// <summary>
    /// 最大值(用于数值类型)
    /// </summary>
    public double? MaxValue { get; set; }

    /// <summary>
    /// 正则表达式验证(用于字符串类型)
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
