// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板上下文工厂
/// </summary>
public interface ITemplateContextFactory
{
    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <returns>模板上下文</returns>
    ITemplateContext CreateContext();

    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <param name="variables">初始变量</param>
    /// <returns>模板上下文</returns>
    ITemplateContext CreateContext(IDictionary<string, object?> variables);

    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <param name="model">模型对象</param>
    /// <returns>模板上下文</returns>
    ITemplateContext CreateContext(object model);

    /// <summary>
    /// 创建构建器
    /// </summary>
    /// <returns>上下文构建器</returns>
    ITemplateContextBuilder CreateBuilder();
}
