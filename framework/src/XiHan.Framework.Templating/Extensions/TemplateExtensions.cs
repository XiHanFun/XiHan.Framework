#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateExtensions
// Guid:9q4s8s3o-7r0t-8p3q-4s9s-8o3r0t7q4s9s
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;
using XiHan.Framework.Templating.Services;

namespace XiHan.Framework.Templating.Extensions;

/// <summary>
/// 模板扩展方法
/// </summary>
public static class TemplateExtensions
{
    /// <summary>
    /// 使用模型渲染模板字符串
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="model">模型对象</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>渲染后的字符串</returns>
    public static async Task<string> RenderAsync(this string template, object? model, IServiceProvider serviceProvider)
    {
        var templateService = serviceProvider.GetRequiredService<ITemplateService>();
        return await templateService.RenderAsync(template, model);
    }

    /// <summary>
    /// 使用变量字典渲染模板字符串
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="variables">变量字典</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>渲染后的字符串</returns>
    public static async Task<string> RenderAsync(this string template, IDictionary<string, object?> variables, IServiceProvider serviceProvider)
    {
        var templateService = serviceProvider.GetRequiredService<ITemplateService>();
        return await templateService.RenderAsync(template, variables);
    }

    /// <summary>
    /// 同步渲染模板字符串
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="model">模型对象</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>渲染后的字符串</returns>
    public static string Render(this string template, object? model, IServiceProvider serviceProvider)
    {
        var contextFactory = serviceProvider.GetRequiredService<ITemplateContextFactory>();
        var engineRegistry = serviceProvider.GetRequiredService<ITemplateEngineRegistry>();

        var context = model != null
        ? contextFactory.CreateContext(model)
        : contextFactory.CreateContext();
        var engine = engineRegistry.GetDefaultEngine<string>() ?? throw new InvalidOperationException("没有找到可用的模板引擎");
        return engine.Render(template, context);
    }

    /// <summary>
    /// 同步渲染模板字符串
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="variables">变量字典</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>渲染后的字符串</returns>
    public static string Render(this string template, IDictionary<string, object?> variables, IServiceProvider serviceProvider)
    {
        var contextFactory = serviceProvider.GetRequiredService<ITemplateContextFactory>();
        var engineRegistry = serviceProvider.GetRequiredService<ITemplateEngineRegistry>();

        var context = contextFactory.CreateContext(variables);
        var engine = engineRegistry.GetDefaultEngine<string>() ?? throw new InvalidOperationException("没有找到可用的模板引擎");
        return engine.Render(template, context);
    }

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="template">模板字符串</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>验证结果</returns>
    public static TemplateValidationResult Validate(this string template, IServiceProvider serviceProvider)
    {
        var templateService = serviceProvider.GetRequiredService<ITemplateService>();
        return templateService.ValidateTemplate(template);
    }

    /// <summary>
    /// 向模板上下文添加变量
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <param name="variables">变量字典</param>
    /// <returns>模板上下文</returns>
    public static ITemplateContext AddVariables(this ITemplateContext context, IDictionary<string, object?> variables)
    {
        foreach (var (key, value) in variables)
        {
            context.SetVariable(key, value);
        }
        return context;
    }

    /// <summary>
    /// 向模板上下文添加对象属性
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <param name="obj">对象</param>
    /// <param name="prefix">属性前缀</param>
    /// <returns>模板上下文</returns>
    public static ITemplateContext AddObject(this ITemplateContext context, object obj, string? prefix = null)
    {
        if (obj == null)
        {
            return context;
        }

        var properties = obj.GetType().GetProperties();
        foreach (var property in properties)
        {
            if (property.CanRead)
            {
                var name = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                var value = property.GetValue(obj);
                context.SetVariable(name, value);
            }
        }

        return context;
    }

    /// <summary>
    /// 创建作用域并执行操作
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <param name="action">操作</param>
    /// <returns>操作结果</returns>
    public static T WithScope<T>(this ITemplateContext context, Func<ITemplateContext, T> action)
    {
        using var scope = context.PushScope();
        return action(context);
    }

    /// <summary>
    /// 创建作用域并执行异步操作
    /// </summary>
    /// <param name="context">模板上下文</param>
    /// <param name="action">异步操作</param>
    /// <returns>操作结果</returns>
    public static async Task<T> WithScopeAsync<T>(this ITemplateContext context, Func<ITemplateContext, Task<T>> action)
    {
        using var scope = context.PushScope();
        return await action(context);
    }
}
