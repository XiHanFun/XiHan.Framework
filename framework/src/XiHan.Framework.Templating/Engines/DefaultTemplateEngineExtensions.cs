#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultTemplateEngineExtensions
// Guid:8f5a2c9e-1b4d-4e7a-9f3c-2d9e1b4a8f3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Engines;

/// <summary>
/// 默认模板引擎扩展方法
/// </summary>
public static class DefaultTemplateEngineExtensions
{
    private static readonly DefaultTemplateEngine DefaultEngine = new();

    #region 字符串扩展方法

    /// <summary>
    /// 使用参数字典渲染字符串模板
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="variables">参数字典</param>
    /// <returns>渲染后的字符串</returns>
    public static string RenderTemplate(this string template, IDictionary<string, object?> variables)
    {
        return DefaultEngine.RenderWithVariables(template, variables);
    }

    /// <summary>
    /// 使用对象属性渲染字符串模板
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="model">对象模型</param>
    /// <returns>渲染后的字符串</returns>
    public static string RenderTemplate(this string template, object? model)
    {
        return DefaultEngine.RenderWithModel(template, model);
    }

    /// <summary>
    /// 使用模板上下文渲染字符串模板
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染后的字符串</returns>
    public static string RenderTemplate(this string template, ITemplateContext context)
    {
        return DefaultEngine.Render(template, context);
    }

    /// <summary>
    /// 验证字符串模板语法
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <returns>验证结果</returns>
    public static TemplateValidationResult ValidateTemplate(this string template)
    {
        return DefaultEngine.Validate(template);
    }

    /// <summary>
    /// 检查字符串模板是否有效
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <returns>是否有效</returns>
    public static bool IsValidTemplate(this string template)
    {
        return DefaultEngine.Validate(template).IsValid;
    }

    #endregion

    #region IDictionary 扩展方法

    /// <summary>
    /// 将字典转换为模板上下文
    /// </summary>
    /// <param name="variables">变量字典</param>
    /// <returns>模板上下文</returns>
    public static ITemplateContext ToTemplateContext(this IDictionary<string, object?> variables)
    {
        var context = new TemplateContext();
        foreach (var kvp in variables)
        {
            context.SetVariable(kvp.Key, kvp.Value);
        }
        return context;
    }

    /// <summary>
    /// 使用字典变量渲染模板
    /// </summary>
    /// <param name="variables">变量字典</param>
    /// <param name="template">模板字符串</param>
    /// <returns>渲染结果</returns>
    public static string RenderTemplate(this IDictionary<string, object?> variables, string template)
    {
        return DefaultEngine.RenderWithVariables(template, variables);
    }

    #endregion

    #region 对象扩展方法

    /// <summary>
    /// 将对象转换为模板上下文
    /// </summary>
    /// <param name="model">对象模型</param>
    /// <returns>模板上下文</returns>
    public static ITemplateContext ToTemplateContext(this object model)
    {
        var context = new TemplateContext();
        var properties = model.GetType().GetProperties();

        foreach (var property in properties)
        {
            context.SetVariable(property.Name, property.GetValue(model));
        }

        return context;
    }

    /// <summary>
    /// 使用对象模型渲染模板
    /// </summary>
    /// <param name="model">对象模型</param>
    /// <param name="template">模板字符串</param>
    /// <returns>渲染结果</returns>
    public static string RenderTemplate(this object model, string template)
    {
        return DefaultEngine.RenderWithModel(template, model);
    }

    #endregion

    #region 流式 API

    /// <summary>
    /// 创建模板构建器
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <returns>模板构建器</returns>
    public static TemplateBuilder CreateBuilder(this string template)
    {
        return new TemplateBuilder(template, DefaultEngine);
    }

    #endregion
}

/// <summary>
/// 模板构建器（流式API）
/// </summary>
public class TemplateBuilder
{
    private readonly string _template;
    private readonly DefaultTemplateEngine _engine;
    private readonly Dictionary<string, object?> _variables = new();

    internal TemplateBuilder(string template, DefaultTemplateEngine engine)
    {
        _template = template ?? throw new ArgumentNullException(nameof(template));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// 添加变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    /// <returns>构建器实例</returns>
    public TemplateBuilder WithVariable(string name, object? value)
    {
        _variables[name] = value;
        return this;
    }

    /// <summary>
    /// 添加多个变量
    /// </summary>
    /// <param name="variables">变量字典</param>
    /// <returns>构建器实例</returns>
    public TemplateBuilder WithVariables(IDictionary<string, object?> variables)
    {
        foreach (var kvp in variables)
        {
            _variables[kvp.Key] = kvp.Value;
        }
        return this;
    }

    /// <summary>
    /// 添加对象属性作为变量
    /// </summary>
    /// <param name="model">对象模型</param>
    /// <param name="prefix">属性前缀</param>
    /// <returns>构建器实例</returns>
    public TemplateBuilder WithModel(object model, string? prefix = null)
    {
        var properties = model.GetType().GetProperties();
        foreach (var property in properties)
        {
            var name = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            _variables[name] = property.GetValue(model);
        }
        return this;
    }

    /// <summary>
    /// 条件添加变量
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    /// <returns>构建器实例</returns>
    public TemplateBuilder WithVariableIf(bool condition, string name, object? value)
    {
        if (condition)
        {
            _variables[name] = value;
        }
        return this;
    }

    /// <summary>
    /// 条件添加变量（使用函数）
    /// </summary>
    /// <param name="condition">条件函数</param>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    /// <returns>构建器实例</returns>
    public TemplateBuilder WithVariableIf(Func<bool> condition, string name, object? value)
    {
        if (condition())
        {
            _variables[name] = value;
        }
        return this;
    }

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <returns>渲染结果</returns>
    public string Render()
    {
        return _engine.RenderWithVariables(_template, _variables);
    }

    /// <summary>
    /// 异步渲染模板
    /// </summary>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderAsync()
    {
        return await Task.FromResult(Render());
    }

    /// <summary>
    /// 验证模板
    /// </summary>
    /// <returns>验证结果</returns>
    public TemplateValidationResult Validate()
    {
        return _engine.Validate(_template);
    }

    /// <summary>
    /// 获取所有变量
    /// </summary>
    /// <returns>变量字典</returns>
    public IDictionary<string, object?> GetVariables()
    {
        return new Dictionary<string, object?>(_variables);
    }

    /// <summary>
    /// 清空所有变量
    /// </summary>
    /// <returns>构建器实例</returns>
    public TemplateBuilder Clear()
    {
        _variables.Clear();
        return this;
    }

    /// <summary>
    /// 克隆构建器
    /// </summary>
    /// <returns>新的构建器实例</returns>
    public TemplateBuilder Clone()
    {
        var cloned = new TemplateBuilder(_template, _engine);
        foreach (var kvp in _variables)
        {
            cloned._variables[kvp.Key] = kvp.Value;
        }
        return cloned;
    }
}
