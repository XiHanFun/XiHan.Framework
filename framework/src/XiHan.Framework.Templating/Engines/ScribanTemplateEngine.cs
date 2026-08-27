// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Scriban;
using Scriban.Runtime;
using XiHan.Framework.Templating.Contexts;
using TemplateContext = XiHan.Framework.Templating.Contexts.TemplateContext;

namespace XiHan.Framework.Templating.Engines;

/// <summary>
/// 基于 Scriban 的模板引擎实现
/// </summary>
public class ScribanTemplateEngine : ITemplateEngine<Template>
{
    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderAsync(Template template, ITemplateContext context, CancellationToken cancellationToken = default)
    {
        var scribanContext = CreateScribanContext(context);
        return await template.RenderAsync(scribanContext);
    }

    /// <summary>
    /// 渲染模板（同步）
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    public string Render(Template template, ITemplateContext context)
    {
        var scribanContext = CreateScribanContext(context);
        return template.Render(scribanContext);
    }

    /// <summary>
    /// 解析模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>解析后的模板</returns>
    public Template Parse(string templateSource)
    {
        return Template.Parse(templateSource);
    }

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    public TemplateValidationResult Validate(string templateSource)
    {
        try
        {
            var template = Template.Parse(templateSource);
            if (template.HasErrors)
            {
                var errors = template.Messages
                    .Where(m => m.Type == Scriban.Parsing.ParserMessageType.Error)
                    .Select(m => m.ToString())
                    .ToList();

                var firstError = template.Messages.FirstOrDefault(m => m.Type == Scriban.Parsing.ParserMessageType.Error);
                return TemplateValidationResult.Failure(
                    string.Join("; ", errors),
                    firstError?.Span.Start.Line + 1,
                    firstError?.Span.Start.Column + 1
                );
            }

            return TemplateValidationResult.Success;
        }
        catch (Exception ex)
        {
            return TemplateValidationResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// 创建 Scriban 上下文
    /// </summary>
    /// <param name="templateContext">模板上下文</param>
    /// <returns>Scriban 上下文</returns>
    /// <remarks>
    /// 返回类型必须写全限定的 <see cref="Scriban.TemplateContext"/>：本文件顶部有
    /// <c>using TemplateContext = XiHan.Framework.Templating.Contexts.TemplateContext;</c> 别名，
    /// 早先这里写裸名 TemplateContext，实际 new 出来的是框架自己的上下文对象，
    /// 交给 Scriban 后被当成一个普通模型对象按属性名取值，
    /// 于是上下文里 SetVariable 进去的变量在模板里一个也取不到，渲染结果里全是空——
    /// 上下文到 Scriban 的桥接等于从未生效。
    /// </remarks>
    private static Scriban.TemplateContext CreateScribanContext(ITemplateContext templateContext)
    {
        var globals = new ScriptObject();

        // 添加变量
        foreach (var variableName in templateContext.GetVariableNames())
        {
            globals[variableName] = templateContext.GetVariable(variableName);
        }

        // 添加函数
        foreach (var variableName in templateContext.GetVariableNames())
        {
            var function = templateContext.GetFunction(variableName);
            if (function != null)
            {
                globals.Import(variableName, function);
            }
        }

        var scribanContext = new Scriban.TemplateContext();
        scribanContext.PushGlobal(globals);

        return scribanContext;
    }
}
