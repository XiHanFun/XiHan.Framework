// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;

namespace XiHan.Framework.Templating.Engines;

/// <summary>
/// 模板引擎接口
/// </summary>
/// <typeparam name="T">模板类型</typeparam>
public interface ITemplateEngine<T>
{
    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderAsync(T template, ITemplateContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// 渲染模板（同步）
    /// </summary>
    /// <param name="template">模板内容</param>
    /// <param name="context">模板上下文</param>
    /// <returns>渲染结果</returns>
    string Render(T template, ITemplateContext context);

    /// <summary>
    /// 解析模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>解析后的模板</returns>
    T Parse(string templateSource);

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    TemplateValidationResult Validate(string templateSource);
}
