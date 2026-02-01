#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ScribanTemplateEngine
// Guid:66cd4d42-37a5-441d-8a64-51cb1b00e8ea
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Scriban;
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
    private static TemplateContext CreateScribanContext(ITemplateContext templateContext)
    {
        var scribanContext = new TemplateContext();

        // 添加变量
        foreach (var variableName in templateContext.GetVariableNames())
        {
            var value = templateContext.GetVariable(variableName);
            scribanContext.SetVariable(variableName, value);
        }

        // 添加函数
        foreach (var variableName in templateContext.GetVariableNames())
        {
            var function = templateContext.GetFunction(variableName);
            if (function != null)
            {
                scribanContext.SetFunction(variableName, function);
            }
        }

        return scribanContext;
    }
}
