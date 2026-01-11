#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultTemplateEngine
// Guid:6a4c9b3d-89a0-40aa-938f-e4c081bdcd31
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 3:44:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Engines;

/// <summary>
/// 默认模板引擎
/// </summary>
public class DefaultTemplateEngine : ITemplateEngine<string>
{
    private readonly ConcurrentDictionary<string, string> _templateCache = new();
    private readonly ConcurrentDictionary<string, TemplateValidationResult> _validationCache = new();

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderAsync(string template, ITemplateContext context, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(Render(template, context));
    }

    /// <summary>
    /// 渲染模板（同步）
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    public string Render(string template, ITemplateContext context)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        // 从模板上下文提取变量
        var variables = ExtractVariables(context);

        // 使用高级模板渲染（支持条件和循环）
        return RenderAdvanced(template, variables);
    }

    /// <summary>
    /// 解析模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>解析后的模板</returns>
    public string Parse(string templateSource)
    {
        return templateSource ?? string.Empty;
    }

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    public TemplateValidationResult Validate(string templateSource)
    {
        if (string.IsNullOrEmpty(templateSource))
        {
            return TemplateValidationResult.Success;
        }

        // 检查缓存
        if (_validationCache.TryGetValue(templateSource, out var cachedResult))
        {
            return cachedResult;
        }

        var result = ValidateTemplateInternal(templateSource);

        // 缓存验证结果
        _validationCache.TryAdd(templateSource, result);

        return result;
    }

    #region 私有方法

    /// <summary>
    /// 从模板上下文中提取变量
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <returns>变量字典</returns>
    private static Dictionary<string, object?> ExtractVariables(ITemplateContext context)
    {
        var variables = new Dictionary<string, object?>();

        var variableNames = context.GetVariableNames();
        foreach (var name in variableNames)
        {
            variables[name] = context.GetVariable(name);
        }

        return variables;
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

    /// <summary>
    /// 内部模板验证方法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    private static TemplateValidationResult ValidateTemplateInternal(string templateSource)
    {
        try
        {
            // 验证基本语法
            if (!ValidateBasicSyntax(templateSource))
            {
                return TemplateValidationResult.Failure("模板语法错误：变量占位符格式不正确");
            }

            // 验证条件语句
            if (!ValidateConditionalSyntax(templateSource))
            {
                return TemplateValidationResult.Failure("模板语法错误：条件语句格式不正确");
            }

            // 验证循环语句
            if (!ValidateLoopSyntax(templateSource))
            {
                return TemplateValidationResult.Failure("模板语法错误：循环语句格式不正确");
            }

            return TemplateValidationResult.Success;
        }
        catch (Exception ex)
        {
            return TemplateValidationResult.Failure($"模板验证过程中发生错误：{ex.Message}");
        }
    }

    /// <summary>
    /// 验证基本语法
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <returns>是否有效</returns>
    private static bool ValidateBasicSyntax(string template)
    {
        // 检查变量占位符的匹配
        var openCount = 0;
        var closeCount = 0;

        for (var i = 0; i < template.Length - 1; i++)
        {
            if (template[i] == '{' && template[i + 1] == '{')
            {
                openCount++;
                i++; // 跳过下一个字符
            }
        }

        for (var i = 0; i < template.Length - 1; i++)
        {
            if (template[i] == '}' && template[i + 1] == '}')
            {
                closeCount++;
                i++; // 跳过下一个字符
            }
        }

        return openCount == closeCount;
    }

    /// <summary>
    /// 验证条件语句语法
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <returns>是否有效</returns>
    private static bool ValidateConditionalSyntax(string template)
    {
        // 验证 if/else/endif 匹配
        var ifMatches = Regex.Matches(template, @"{{if\s+[^}]+}}");
        var endifMatches = Regex.Matches(template, @"{{endif}}");

        return ifMatches.Count == endifMatches.Count;
    }

    /// <summary>
    /// 验证循环语句语法
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <returns>是否有效</returns>
    private static bool ValidateLoopSyntax(string template)
    {
        // 验证 for/endfor 匹配
        var forMatches = Regex.Matches(template, @"{{for\s+[^}]+\s+in\s+[^}]+}}");
        var endForMatches = Regex.Matches(template, @"{{endfor}}");

        return forMatches.Count == endForMatches.Count;
    }

    /// <summary>
    /// 渲染包含条件语句的模板（集成Simple功能）
    /// </summary>
    /// <param name="template">模板文本，支持 {{if condition}}...{{else}}...{{endif}} 条件语句</param>
    /// <param name="values">变量值字典</param>
    /// <returns>渲染后的文本</returns>
    private string RenderAdvanced(string template, IDictionary<string, object?>? values)
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
    private string ProcessConditionals(string template, IDictionary<string, object?> values)
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
    private string ProcessLoops(string template, IDictionary<string, object?> values)
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

    #endregion

    #region 公共辅助方法

    /// <summary>
    /// 获取缓存的模板
    /// </summary>
    /// <param name="key">模板键</param>
    /// <returns>模板内容</returns>
    public string? GetCachedTemplate(string key)
    {
        return _templateCache.TryGetValue(key, out var template) ? template : null;
    }

    /// <summary>
    /// 设置缓存模板
    /// </summary>
    /// <param name="key">模板键</param>
    /// <param name="template">模板内容</param>
    public void SetCachedTemplate(string key, string template)
    {
        _templateCache.AddOrUpdate(key, template, (_, _) => template);
    }

    /// <summary>
    /// 移除缓存模板
    /// </summary>
    /// <param name="key">模板键</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveCachedTemplate(string key)
    {
        return _templateCache.TryRemove(key, out _);
    }

    /// <summary>
    /// 清空模板缓存
    /// </summary>
    public void ClearTemplateCache()
    {
        _templateCache.Clear();
        _validationCache.Clear();
    }

    /// <summary>
    /// 使用缓存渲染模板
    /// </summary>
    /// <param name="key">模板键</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    public string? RenderCached(string key, ITemplateContext context)
    {
        var template = GetCachedTemplate(key);
        return string.IsNullOrEmpty(template) ? null : Render(template, context);
    }

    #endregion

    #region 文件模板处理

    /// <summary>
    /// 从文件渲染模板
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="context">模板上下文</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染结果</returns>
    public string RenderFile(string templateFilePath, ITemplateContext context, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = File.ReadAllText(templateFilePath, encoding);
        return Render(template, context);
    }

    /// <summary>
    /// 异步从文件渲染模板
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="context">模板上下文</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderFileAsync(string templateFilePath, ITemplateContext context, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = await File.ReadAllTextAsync(templateFilePath, encoding, cancellationToken);
        return await RenderAsync(template, context, cancellationToken);
    }

    /// <summary>
    /// 渲染模板并保存到文件
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="outputFilePath">输出文件路径</param>
    /// <param name="context">模板上下文</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    public void RenderToFile(string templateFilePath, string outputFilePath, ITemplateContext context, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var renderedContent = RenderFile(templateFilePath, context, encoding);

        var outputDirectory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputFilePath, renderedContent, encoding);
    }

    /// <summary>
    /// 异步渲染模板并保存到文件
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="outputFilePath">输出文件路径</param>
    /// <param name="context">模板上下文</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RenderToFileAsync(string templateFilePath, string outputFilePath, ITemplateContext context, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        encoding ??= Encoding.UTF8;

        var renderedContent = await RenderFileAsync(templateFilePath, context, encoding, cancellationToken);

        var outputDirectory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await File.WriteAllTextAsync(outputFilePath, renderedContent, encoding, cancellationToken);
    }

    /// <summary>
    /// 获取或加载文件模板并渲染
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="context">模板上下文</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染结果</returns>
    public string GetOrLoadAndRender(string cacheKey, string templateFilePath, ITemplateContext context, Encoding? encoding = null)
    {
        var template = GetOrLoadTemplate(cacheKey, templateFilePath, encoding);
        return Render(template, context);
    }

    /// <summary>
    /// 获取或加载文件模板
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>模板内容</returns>
    public string GetOrLoadTemplate(string cacheKey, string templateFilePath, Encoding? encoding = null)
    {
        if (_templateCache.TryGetValue(cacheKey, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = File.ReadAllText(templateFilePath, encoding);
        SetCachedTemplate(cacheKey, template);

        return template;
    }

    /// <summary>
    /// 异步获取或加载文件模板
    /// </summary>
    /// <param name="cacheKey">缓存键</param>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模板内容</returns>
    public async Task<string> GetOrLoadTemplateAsync(string cacheKey, string templateFilePath, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        if (_templateCache.TryGetValue(cacheKey, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = await File.ReadAllTextAsync(templateFilePath, encoding, cancellationToken);
        SetCachedTemplate(cacheKey, template);

        return template;
    }

    /// <summary>
    /// 验证文件模板
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>验证结果</returns>
    public TemplateValidationResult ValidateFile(string templateFilePath, Encoding? encoding = null)
    {
        try
        {
            encoding ??= Encoding.UTF8;

            if (!File.Exists(templateFilePath))
            {
                return TemplateValidationResult.Failure($"模板文件不存在: {templateFilePath}");
            }

            var template = File.ReadAllText(templateFilePath, encoding);
            return Validate(template);
        }
        catch (Exception ex)
        {
            return TemplateValidationResult.Failure($"读取模板文件时发生错误: {ex.Message}");
        }
    }

    #endregion

    #region 便捷方法

    /// <summary>
    /// 使用变量字典渲染模板
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="variables">变量字典</param>
    /// <returns>渲染结果</returns>
    public string RenderWithVariables(string template, IDictionary<string, object?> variables)
    {
        return RenderAdvanced(template, variables);
    }

    /// <summary>
    /// 使用对象模型渲染模板
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="model">对象模型</param>
    /// <returns>渲染结果</returns>
    public string RenderWithModel(string template, object? model)
    {
        if (model == null)
        {
            return template;
        }

        var properties = model.GetType().GetProperties();
        var variables = new Dictionary<string, object?>();

        foreach (var property in properties)
        {
            variables.Add(property.Name, property.GetValue(model));
        }

        return RenderAdvanced(template, variables);
    }

    /// <summary>
    /// 从文件使用变量字典渲染模板
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="variables">变量字典</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染结果</returns>
    public string RenderFileWithVariables(string templateFilePath, IDictionary<string, object?> variables, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = File.ReadAllText(templateFilePath, encoding);
        return RenderAdvanced(template, variables);
    }

    /// <summary>
    /// 从文件使用对象模型渲染模板
    /// </summary>
    /// <param name="templateFilePath">模板文件路径</param>
    /// <param name="model">对象模型</param>
    /// <param name="encoding">文件编码，默认为UTF8</param>
    /// <returns>渲染结果</returns>
    public string RenderFileWithModel(string templateFilePath, object model, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
        {
            throw new FileNotFoundException("模板文件不存在", templateFilePath);
        }

        var template = File.ReadAllText(templateFilePath, encoding);
        return RenderWithModel(template, model);
    }

    #endregion
}
