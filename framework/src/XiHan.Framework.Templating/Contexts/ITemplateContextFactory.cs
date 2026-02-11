#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateContextFactory
// Guid:96dc2b13-d33e-4f38-8b3a-0bde22835458
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 03:53:48
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
