// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;
using XiHan.Framework.Templating.Services;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// 手写的 <see cref="ITemplateService"/> 替身
/// </summary>
/// <remarks>
/// 只做 <c>{{属性名}}</c> 的字面替换：命中的占位符替换为属性值，未命中的占位符原样保留。
/// 真实 Templating 引擎需要模块初始化阶段把引擎注册进 <c>ITemplateEngineRegistry</c> 才可用，
/// 单测里不做模块启动，所以用替身固定渲染行为，把断言聚焦在 Bot 侧的编排上。
/// </remarks>
public sealed class FakeTemplateService : ITemplateService
{
    private readonly List<string> _renderedSources = [];

    /// <summary>
    /// 依次收到的模板源码
    /// </summary>
    public IReadOnlyList<string> RenderedSources => _renderedSources;

    /// <summary>
    /// 最后一次收到的模型
    /// </summary>
    public object? LastModel { get; private set; }

    /// <summary>
    /// 渲染次数
    /// </summary>
    public int RenderCount => _renderedSources.Count;

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="model">模型对象</param>
    public Task<string> RenderAsync(string templateSource, object? model = null)
    {
        _renderedSources.Add(templateSource);
        LastModel = model;
        return Task.FromResult(Substitute(templateSource, model));
    }

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="variables">变量字典</param>
    public Task<string> RenderAsync(string templateSource, IDictionary<string, object?> variables)
    {
        _renderedSources.Add(templateSource);
        LastModel = variables;

        var result = templateSource;
        foreach (var pair in variables)
        {
            result = result.Replace("{{" + pair.Key + "}}", pair.Value?.ToString() ?? string.Empty);
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// 从文件渲染模板
    /// </summary>
    /// <param name="templatePath">模板文件路径</param>
    /// <param name="model">模型对象</param>
    public Task<string> RenderFileAsync(string templatePath, object? model = null)
    {
        throw new NotSupportedException("测试替身不读取文件模板。");
    }

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    public TemplateValidationResult ValidateTemplate(string templateSource)
    {
        return TemplateValidationResult.Success;
    }

    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <param name="model">模型对象</param>
    public ITemplateContext CreateContext(object? model = null)
    {
        throw new NotSupportedException("测试替身不提供模板上下文。");
    }

    private static string Substitute(string source, object? model)
    {
        if (model is null)
        {
            return source;
        }

        var result = source;
        foreach (var property in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
            {
                continue;
            }

            result = result.Replace("{{" + property.Name + "}}", property.GetValue(model)?.ToString() ?? string.Empty);
        }

        return result;
    }
}
