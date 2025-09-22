#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITemplateContext
// Guid:6d1f5f0b-4e7g-5c0d-1f6f-5b0e7g4d1f6f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 3:42:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Abstractions;

/// <summary>
/// 模板上下文接口
/// </summary>
public interface ITemplateContext
{
    /// <summary>
    /// 获取变量值
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>变量值</returns>
    object? GetVariable(string name);

    /// <summary>
    /// 设置变量值
    /// </summary>
    /// <param name="name">变量名</param>
    /// <param name="value">变量值</param>
    void SetVariable(string name, object? value);

    /// <summary>
    /// 是否包含变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>是否包含</returns>
    bool HasVariable(string name);

    /// <summary>
    /// 移除变量
    /// </summary>
    /// <param name="name">变量名</param>
    /// <returns>是否成功移除</returns>
    bool RemoveVariable(string name);

    /// <summary>
    /// 获取所有变量名
    /// </summary>
    /// <returns>变量名集合</returns>
    IEnumerable<string> GetVariableNames();

    /// <summary>
    /// 推入作用域
    /// </summary>
    /// <returns>作用域标识</returns>
    IDisposable PushScope();

    /// <summary>
    /// 获取函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <returns>函数委托</returns>
    Delegate? GetFunction(string name);

    /// <summary>
    /// 设置函数
    /// </summary>
    /// <param name="name">函数名</param>
    /// <param name="function">函数委托</param>
    void SetFunction(string name, Delegate function);

    /// <summary>
    /// 克隆上下文
    /// </summary>
    /// <returns>新的上下文实例</returns>
    ITemplateContext Clone();
}

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

/// <summary>
/// 模板作用域
/// </summary>
public interface ITemplateScope : IDisposable
{
    /// <summary>
    /// 作用域标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 父作用域
    /// </summary>
    ITemplateScope? Parent { get; }

    /// <summary>
    /// 是否为根作用域
    /// </summary>
    bool IsRoot { get; }

    /// <summary>
    /// 作用域变量
    /// </summary>
    IDictionary<string, object?> Variables { get; }
}

/// <summary>
/// 模板上下文访问器
/// </summary>
public interface ITemplateContextAccessor
{
    /// <summary>
    /// 当前模板上下文
    /// </summary>
    ITemplateContext? Current { get; set; }
}

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
