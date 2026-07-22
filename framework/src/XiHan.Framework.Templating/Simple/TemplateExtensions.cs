// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Simple;

/// <summary>
/// 模板引擎扩展方法
/// </summary>
public static class TemplateExtensions
{
    /// <summary>
    /// 使用参数字典渲染模板字符串
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="values">参数字典</param>
    /// <returns>渲染后的字符串</returns>
    public static string RenderTemplate(this string template, IDictionary<string, object?> values)
    {
        return TemplateEngine.Render(template, values);
    }

    /// <summary>
    /// 使用对象属性渲染模板字符串
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="model">对象模型</param>
    /// <returns>渲染后的字符串</returns>
    public static string RenderTemplate(this string template, object? model)
    {
        return TemplateEngine.Render(template, model);
    }

    /// <summary>
    /// 使用参数字典渲染高级模板字符串，支持条件和循环语句
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="values">参数字典</param>
    /// <returns>渲染后的字符串</returns>
    public static string RenderAdvancedTemplate(this string template, IDictionary<string, object?> values)
    {
        return TemplateEngine.RenderAdvanced(template, values);
    }
}
