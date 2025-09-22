#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateContextBuilder
// Guid:7e0f69a9-56d6-44bf-9163-890f64aba555
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 3:52:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板上下文构建器
/// </summary>
public interface ITemplateContextBuilder
{
    /// <summary>
    /// 添加变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    /// <returns>构建器实例</returns>
    ITemplateContextBuilder AddVariable(string name, object? value);

    /// <summary>
    /// 添加变量字典
    /// </summary>
    /// <param name="variables">变量字典</param>
    /// <returns>构建器实例</returns>
    ITemplateContextBuilder AddVariables(IDictionary<string, object?> variables);

    /// <summary>
    /// 添加对象属性
    /// </summary>
    /// <param name="obj">对象</param>
    /// <param name="prefix">属性前缀</param>
    /// <returns>构建器实例</returns>
    ITemplateContextBuilder AddObject(object obj, string? prefix = null);

    /// <summary>
    /// 添加函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="function">函数委托</param>
    /// <returns>构建器实例</returns>
    ITemplateContextBuilder AddFunction(string name, Delegate function);

    /// <summary>
    /// 添加全局函数
    /// </summary>
    /// <param name="type">包含静态方法的类型</param>
    /// <returns>构建器实例</returns>
    ITemplateContextBuilder AddGlobalFunctions(Type type);

    /// <summary>
    /// 构建上下文
    /// </summary>
    /// <returns>模板上下文</returns>
    ITemplateContext Build();
}
