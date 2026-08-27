// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Models;
using XiHan.Framework.Bot.Template;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// 手写的 <see cref="IBotTemplateEngine"/> 替身
/// </summary>
public sealed class FakeBotTemplateEngine : IBotTemplateEngine
{
    /// <summary>
    /// 预置渲染结果
    /// </summary>
    public BotMessage Message { get; set; } = new() { Content = "rendered" };

    /// <summary>
    /// 最后一次按名称渲染时收到的模板名
    /// </summary>
    public string? LastTemplateName { get; private set; }

    /// <summary>
    /// 最后一次渲染时收到的模型
    /// </summary>
    public object? LastModel { get; private set; }

    /// <summary>
    /// 渲染次数
    /// </summary>
    public int RenderCount { get; private set; }

    /// <summary>
    /// 按名称渲染模板
    /// </summary>
    /// <param name="templateName">模板名称</param>
    /// <param name="model">模板模型</param>
    public Task<BotMessage> RenderAsync(string templateName, object? model = null)
    {
        RenderCount++;
        LastTemplateName = templateName;
        LastModel = model;
        return Task.FromResult(Message);
    }

    /// <summary>
    /// 渲染模板实例
    /// </summary>
    /// <param name="template">模板</param>
    /// <param name="model">模板模型</param>
    public Task<BotMessage> RenderAsync(BotTemplate template, object? model = null)
    {
        RenderCount++;
        LastTemplateName = template.Name;
        LastModel = model;
        return Task.FromResult(Message);
    }
}
