#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateService
// Guid:ee1509d1-d05e-4217-b240-cc2e9a828741
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:48:19
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Services;

/// <summary>
/// 模板服务
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="model">模型对象</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderAsync(string templateSource, object? model = null);

    /// <summary>
    /// 渲染模板
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <param name="variables">变量字典</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderAsync(string templateSource, IDictionary<string, object?> variables);

    /// <summary>
    /// 从文件渲染模板
    /// </summary>
    /// <param name="templatePath">模板文件路径</param>
    /// <param name="model">模型对象</param>
    /// <returns>渲染结果</returns>
    Task<string> RenderFileAsync(string templatePath, object? model = null);

    /// <summary>
    /// 验证模板语法
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    /// <returns>验证结果</returns>
    TemplateValidationResult ValidateTemplate(string templateSource);

    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <param name="model">模型对象</param>
    /// <returns>模板上下文</returns>
    ITemplateContext CreateContext(object? model = null);
}
