#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateService
// Guid:8p3r7r2n-6q9s-7o2p-3r8r-7n2q9s6p3r8r
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Services;

/// <summary>
/// 模板服务实现
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly ITemplateEngineRegistry _engineRegistry;
    private readonly ITemplateContextFactory _contextFactory;
    private readonly TemplatingOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="engineRegistry">模板引擎注册表</param>
    /// <param name="contextFactory">模板上下文工厂</param>
    /// <param name="options">模板选项</param>
    public TemplateService(
        ITemplateEngineRegistry engineRegistry,
        ITemplateContextFactory contextFactory,
        IOptions<TemplatingOptions> options)
    {
        _engineRegistry = engineRegistry;
        _contextFactory = contextFactory;
        _options = options.Value;
    }

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="model">模型对象</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderAsync(string templateSource, object? model = null)
    {
        var context = model != null
        ? _contextFactory.CreateContext(model)
        : _contextFactory.CreateContext();
        var engine = _engineRegistry.GetDefaultEngine<string>() ?? throw new InvalidOperationException("没有找到可用的模板引擎");
        return await engine.RenderAsync(templateSource, context);
    }

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="variables">变量字典</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderAsync(string templateSource, IDictionary<string, object?> variables)
    {
        var context = _contextFactory.CreateContext(variables);
        var engine = _engineRegistry.GetDefaultEngine<string>() ?? throw new InvalidOperationException("没有找到可用的模板引擎");
        return await engine.RenderAsync(templateSource, context);
    }

    /// <summary>
    /// 从文件渲染模板
    /// </summary>
    /// <param name="templatePath">模板文件路径</param>
    /// <param name="model">模型对象</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderFileAsync(string templatePath, object? model = null)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"模板文件不存在: {templatePath}");
        }

        var templateSource = await File.ReadAllTextAsync(templatePath);
        return await RenderAsync(templateSource, model);
    }

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    public TemplateValidationResult ValidateTemplate(string templateSource)
    {
        var engine = _engineRegistry.GetDefaultEngine<string>() ?? throw new InvalidOperationException("没有找到可用的模板引擎");
        return engine.Validate(templateSource);
    }

    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <param name="model">模型对象</param>
    /// <returns>模板上下文</returns>
    public ITemplateContext CreateContext(object? model = null)
    {
        return model != null ? _contextFactory.CreateContext(model) : _contextFactory.CreateContext();
    }
}
