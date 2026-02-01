#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateEngine
// Guid:d348e7c5-1c2f-4ba9-8ec9-23c6e6b89a30
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/27 01:40:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Text;
using System.Text.RegularExpressions;

namespace XiHan.Framework.Templating.Simple;

/// <summary>
/// 模板引擎
/// </summary>
public static class TemplateEngine
{
    #region 基础模板渲染

    /// <summary>
    /// 渲染简单模板
    /// </summary>
    /// <param name="template">模板文本，使用 {{变量名}} 作为占位符</param>
    /// <param name="values">变量值字典</param>
    /// <returns>渲染后的文本</returns>
    public static string Render(string template, IDictionary<string, object?>? values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        if (values == null || values.Count == 0)
        {
            return template;
        }

        var result = template;
        foreach (var pair in values)
        {
            var placeholder = $"{{{{{pair.Key}}}}}";
            var value = pair.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value);
        }

        return result;
    }

    /// <summary>
    /// 渲染简单模板
    /// </summary>
    /// <param name="template">模板文本，使用 {{变量名}} 作为占位符</param>
    /// <param name="model">包含属性的对象模型</param>
    /// <returns>渲染后的文本</returns>
    public static string Render(string template, object? model)
    {
        if (string.IsNullOrEmpty(template) || model == null)
        {
            return template;
        }

        var properties = model.GetType().GetProperties();
        var values = new Dictionary<string, object?>();

        foreach (var property in properties)
        {
            values.Add(property.Name, property.GetValue(model));
        }

        return Render(template, values);
    }

    #endregion 基础模板渲染

    #region 高级模板渲染

    /// <summary>
    /// 渲染包含条件语句的模板
    /// </summary>
    /// <param name="template">模板文本，支持 {{if condition}}...{{else}}...{{endif}} 条件语句</param>
    /// <param name="values">变量值字典</param>
    /// <returns>渲染后的文本</returns>
    public static string RenderAdvanced(string template, IDictionary<string, object?>? values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        if (values == null || values.Count == 0)
        {
            return template;
        }

        // 先处理条件语句
        var result = ProcessConditionals(template, values);

        // 再处理循环语句
        result = ProcessLoops(result, values);

        // 最后替换普通变量
        foreach (var pair in values)
        {
            var placeholder = $"{{{{{pair.Key}}}}}";
            var value = pair.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value);
        }

        return result;
    }

    /// <summary>
    /// 处理条件语句
    /// </summary>
    /// <param name="template">模板文本</param>
    /// <param name="values">变量值字典</param>
    /// <returns>处理后的文本</returns>
    private static string ProcessConditionals(string template, IDictionary<string, object?> values)
    {
        var ifPattern = @"{{if\s+([^}]+)}}(.*?)(?:{{else}}(.*?))?{{endif}}";
        var result = template;

        while (Regex.IsMatch(result, ifPattern, RegexOptions.Singleline))
        {
            result = Regex.Replace(result, ifPattern, match =>
            {
                var condition = match.Groups[1].Value.Trim();
                var trueBlock = match.Groups[2].Value;
                var falseBlock = match.Groups.Count > 3 ? match.Groups[3].Value : string.Empty;

                var conditionValue = EvaluateCondition(condition, values);
                return conditionValue ? trueBlock : falseBlock;
            }, RegexOptions.Singleline);
        }

        return result;
    }

    /// <summary>
    /// 处理循环语句
    /// </summary>
    /// <param name="template">模板文本</param>
    /// <param name="values">变量值字典</param>
    /// <returns>处理后的文本</returns>
    private static string ProcessLoops(string template, IDictionary<string, object?> values)
    {
        var forPattern = @"{{for\s+([^}]+)\s+in\s+([^}]+)}}(.*?){{endfor}}";
        var result = template;

        while (Regex.IsMatch(result, forPattern, RegexOptions.Singleline))
        {
            result = Regex.Replace(result, forPattern, match =>
            {
                var itemName = match.Groups[1].Value.Trim();
                var listName = match.Groups[2].Value.Trim();
                var loopContent = match.Groups[3].Value;

                if (!values.TryGetValue(listName, out var listObj) || listObj == null)
                {
                    return string.Empty;
                }

                if (listObj is not IEnumerable enumerable)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();
                foreach (var item in enumerable)
                {
                    // 创建一个新的上下文，包含循环项
                    var loopContext = new Dictionary<string, object?>(values)
                    {
                        [itemName] = item
                    };

                    // 递归处理循环内容
                    var processedContent = RenderAdvanced(loopContent, loopContext);
                    sb.Append(processedContent);
                }

                return sb.ToString();
            }, RegexOptions.Singleline);
        }

        return result;
    }

    /// <summary>
    /// 评估条件表达式
    /// </summary>
    /// <param name="condition">条件表达式</param>
    /// <param name="values">变量值字典</param>
    /// <returns>条件评估结果</returns>
    private static bool EvaluateCondition(string condition, IDictionary<string, object?> values)
    {
        // 支持简单的条件表达式：变量存在性、相等、不等
        if (condition.Contains("=="))
        {
            var parts = condition.Split("==", StringSplitOptions.TrimEntries);
            var leftValue = GetValueFromExpression(parts[0], values);
            var rightValue = GetValueFromExpression(parts[1], values);

            return string.Equals(
                leftValue?.ToString() ?? string.Empty,
                rightValue?.ToString() ?? string.Empty,
                StringComparison.Ordinal);
        }

        if (condition.Contains("!="))
        {
            var parts = condition.Split("!=", StringSplitOptions.TrimEntries);
            var leftValue = GetValueFromExpression(parts[0], values);
            var rightValue = GetValueFromExpression(parts[1], values);

            return !string.Equals(
                leftValue?.ToString() ?? string.Empty,
                rightValue?.ToString() ?? string.Empty,
                StringComparison.Ordinal);
        }

        // 简单的变量存在性检查
        var variableName = condition.Trim();
        return values.TryGetValue(variableName, out var value) && (value is bool boolValue
                ? boolValue
                : value is string strValue
                    ? !string.IsNullOrEmpty(strValue)
                    : value != null);
    }

    /// <summary>
    /// 从表达式获取值
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="values">变量值字典</param>
    /// <returns>表达式计算的值</returns>
    private static object? GetValueFromExpression(string expression, IDictionary<string, object?> values)
    {
        expression = expression.Trim();

        // 检查是否是字符串字面量
        if ((expression.Length > 1 && expression.StartsWith('"') && expression.EndsWith('"')) ||
            (expression.Length > 1 && expression.StartsWith('\'') && expression.EndsWith('\'')))
        {
            return expression[1..^1];
        }

        // 检查是否是数字字面量
        if (int.TryParse(expression, out var intValue))
        {
            return intValue;
        }

        if (double.TryParse(expression, out var doubleValue))
        {
            return doubleValue;
        }

        // 检查是否是布尔字面量
        if (bool.TryParse(expression, out var boolValue))
        {
            return boolValue;
        }

        // 否则视为变量
        return values.TryGetValue(expression, out var value) ? value : null;
    }

    #endregion 高级模板渲染
}
