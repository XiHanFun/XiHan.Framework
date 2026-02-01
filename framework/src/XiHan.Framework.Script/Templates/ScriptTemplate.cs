#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScriptTemplate
// Guid:24bd1b49-9a86-4a09-bd1d-8b9f270e3993
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/02 10:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Script.Templates;

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
                string.IsNullOrWhiteSpace(value.ToString()))
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
