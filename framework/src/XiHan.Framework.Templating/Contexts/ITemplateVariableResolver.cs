// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Contexts;

/// <summary>
/// 模板变量解析器
/// </summary>
public interface ITemplateVariableResolver
{
    /// <summary>
    /// 解析变量表达式
    /// </summary>
    /// <param name="expression">变量表达式</param>
    /// <param name="context">模板上下文</param>
    /// <returns>解析结果</returns>
    object? ResolveVariable(string expression, ITemplateContext context);

    /// <summary>
    /// 解析属性路径
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="propertyPath">属性路径（如：user.profile.name）</param>
    /// <returns>属性值</returns>
    object? ResolvePropertyPath(object target, string propertyPath);

    /// <summary>
    /// 设置属性值
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="propertyPath">属性路径</param>
    /// <param name="value">属性值</param>
    void SetPropertyValue(object target, string propertyPath, object? value);

    /// <summary>
    /// 调用方法
    /// </summary>
    /// <param name="target">目标对象</param>
    /// <param name="methodName">方法名</param>
    /// <param name="arguments">方法参数</param>
    /// <returns>方法返回值</returns>
    object? InvokeMethod(object target, string methodName, params object?[] arguments);
}
