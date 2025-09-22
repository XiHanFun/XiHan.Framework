#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:StringTemplateEngine
// Guid:6a4c9b3d-89a0-40aa-938f-e4c081bdcd31
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 3:44:40
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Engines;

/// <summary>
/// 字符串模板引擎（简化版本）
/// </summary>
public class StringTemplateEngine : ITemplateEngine<string>
{
    private readonly ScribanTemplateEngine _scribanEngine = new();

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染结果</returns>
    public async Task<string> RenderAsync(string template, ITemplateContext context, CancellationToken cancellationToken = default)
    {
        var parsedTemplate = _scribanEngine.Parse(template);
        return await _scribanEngine.RenderAsync(parsedTemplate, context, cancellationToken);
    }

    /// <summary>
    /// 渲染模板（同步）
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    public string Render(string template, ITemplateContext context)
    {
        var parsedTemplate = _scribanEngine.Parse(template);
        return _scribanEngine.Render(parsedTemplate, context);
    }

    /// <summary>
    /// 解析模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>解析后的模板</returns>
    public string Parse(string templateSource)
    {
        return templateSource;
    }

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    public TemplateValidationResult Validate(string templateSource)
    {
        return _scribanEngine.Validate(templateSource);
    }
}
