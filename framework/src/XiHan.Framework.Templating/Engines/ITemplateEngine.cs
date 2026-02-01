#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateEngine
// Guid:3a8f2c9e-1b4d-4e7a-8f3c-2d9e1b4a7f3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
