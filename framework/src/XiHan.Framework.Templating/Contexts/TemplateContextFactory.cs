#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TemplateContextFactory
// Guid:9ade944e-8e67-4a95-987b-3148aed385c8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 03:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Reflection;

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板上下文工厂
/// </summary>
public class TemplateContextFactory : ITemplateContextFactory
{
    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <returns>模板上下文</returns>
    public ITemplateContext CreateContext()
    {
        return new TemplateContext();
    }

    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <param name="variables">初始变量</param>
    /// <returns>模板上下文</returns>
    public ITemplateContext CreateContext(IDictionary<string, object?> variables)
    {
        var context = new TemplateContext();
        foreach (var (key, value) in variables)
        {
            context.SetVariable(key, value);
        }
        return context;
    }

    /// <summary>
    /// 创建模板上下文
    /// </summary>
    /// <param name="model">模型对象</param>
    /// <returns>模板上下文</returns>
    public ITemplateContext CreateContext(object model)
    {
        var context = new TemplateContext();

        if (model != null)
        {
            var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.CanRead)
                {
                    var value = property.GetValue(model);
                    context.SetVariable(property.Name, value);
                }
            }
        }

        return context;
    }

    /// <summary>
    /// 创建构建器
    /// </summary>
    /// <returns>上下文构建器</returns>
    public ITemplateContextBuilder CreateBuilder()
    {
        return new TemplateContextBuilder(this);
    }
}
